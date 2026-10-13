using System.Net;
using System.Net.Http.Json;
using System.Text.Json.Nodes;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NICEDA.ApiService.Crm;
using NICEDA.ApiService.Endpoints;
using NICEDA.ApiService.Legal;
using NICEDA.Web;
using NICEDA.Web.Components.Pages;

namespace NICEDA.Tests;

public sealed class DataExplorerTests
{
    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    public async Task ExplorerReloadsForUserChangesWithoutChangingMode(string mode)
    {
        var demo = new DemoState();
        demo.SetMode(mode);
        using var handler = new ExplorerHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://demo") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(demo);
        services.AddSingleton(new DataExplorerApiClient(client));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());

        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<DataExplorer>(ParameterView.Empty);
            Assert.Contains("Jane record", component.ToHtmlString());
            if (mode == "before")
            {
                Assert.Contains("class=\"truncated-cell-text\"", component.ToHtmlString());
                Assert.Contains("title=\"The full sales note stays available on hover.\"", component.ToHtmlString());
            }
            Assert.Single(handler.Requests);

            demo.SetUser("admin");
            Assert.Contains("Admin record", component.ToHtmlString());
            Assert.DoesNotContain("Jane record", component.ToHtmlString());
            Assert.Equal(2, handler.Requests.Count);

            demo.SetUser("jane");
            Assert.Contains("Jane record", component.ToHtmlString());
            Assert.DoesNotContain("Admin record", component.ToHtmlString());
            Assert.Equal(3, handler.Requests.Count);
            Assert.All(handler.Requests, request => Assert.Equal(
                mode == "after" ? "/api/data-explorer/crm/optimized/customers" : "/api/data-explorer/crm/customers",
                request.Path));
            Assert.Equal(["jane", "admin", "jane"], handler.Requests.Select(request => request.User));
        });
    }

    [Theory]
    [InlineData("CRM", "before", "crm/customers")]
    [InlineData("CRM", "after", "crm/optimized/customers")]
    [InlineData("SQL", "before", "sql/raw/customers")]
    [InlineData("SQL", "after", "sql/optimized/customer-health")]
    [InlineData("Cosmos DB", "before", "cosmos/raw-support")]
    [InlineData("Cosmos DB", "after", "cosmos/optimized-support")]
    [InlineData("SharePoint", "before", "legal/documents")]
    [InlineData("SharePoint", "after", "legal/optimized/documents")]
    public async Task ClientSendsIdentityPerRequest(string system, string mode, string path)
    {
        using var handler = new ExplorerHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://demo") };
        var api = new DataExplorerApiClient(client);
        await api.GetSystemAsync(system, mode, "jane");
        await api.GetSystemAsync(system, mode, "admin");

        Assert.Equal(["jane", "admin"], handler.Requests.Select(request => request.User));
        Assert.All(handler.Requests, request => Assert.Equal($"/api/data-explorer/{path}", request.Path));
        Assert.False(client.DefaultRequestHeaders.Contains("X-Niceda-User"));
    }

    [Fact]
    public async Task RecordEndpointsFilterBothShapesAndRejectMissingOrUnknownIdentity()
    {
        var contentRoot = Path.GetFullPath(
            Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NICEDA.ApiService"));
        var environment = new FakeWebHostEnvironment(contentRoot);
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton(new CrmRepository(environment));
        builder.Services.AddSingleton(new LegalRepository(environment));
        builder.Services.AddScoped(_ => new SqlConnection());
        builder.Services.AddSingleton(_ => new CosmosClient(
            "https://localhost:8081", Convert.ToBase64String(new byte[64])));
        await using var app = builder.Build();
        app.MapDataExplorerEndpoints();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await app.StartAsync(deadline.Token);
        using var client = new HttpClient { BaseAddress = new Uri(app.Urls.Single()) };
        try
        {
            foreach (var path in new[]
            {
                "crm/customers", "crm/optimized/customers", "legal/documents", "legal/optimized/documents",
                "sql/raw/customers", "sql/optimized/customer-health", "sql/optimized/follow-up-tasks",
                "cosmos/raw-support", "cosmos/optimized-support",
            })
            {
                foreach (var user in new[] { "", "unknown" })
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/data-explorer/{path}");
                    if (user.Length > 0) request.Headers.Add("X-Niceda-User", user);
                    using var response = await client.SendAsync(request, deadline.Token);
                    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
                }
            }

            foreach (var path in new[]
            {
                "crm/customers", "crm/optimized/customers", "legal/documents", "legal/optimized/documents",
            })
            {
                foreach (var user in new[] { "jane", "admin" })
                {
                    using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/data-explorer/{path}");
                    request.Headers.Add("X-Niceda-User", user);
                    using var response = await client.SendAsync(request, deadline.Token);
                    response.EnsureSuccessStatusCode();
                    var rows = await response.Content.ReadFromJsonAsync<List<JsonObject>>(deadline.Token);
                    Assert.NotNull(rows);
                    var isCrm = path.StartsWith("crm", StringComparison.Ordinal);
                    Assert.Equal(user == "admin" ? (isCrm ? 5 : 11) : (isCrm ? 3 : 7), rows.Count);
                    if (user == "jane")
                    {
                        var json = System.Text.Json.JsonSerializer.Serialize(rows);
                        Assert.DoesNotContain("contoso", json, StringComparison.OrdinalIgnoreCase);
                        Assert.DoesNotContain("fabrikam", json, StringComparison.OrdinalIgnoreCase);
                        Assert.DoesNotContain("internal", json, StringComparison.OrdinalIgnoreCase);
                    }
                }
            }

            using var inventory = await client.GetAsync("/api/data-explorer/inventory", deadline.Token);
            Assert.Equal(HttpStatusCode.OK, inventory.StatusCode);
        }
        finally
        {
            await app.StopAsync(deadline.Token);
        }
    }

    private sealed class ExplorerHandler : HttpMessageHandler
    {
        public List<(string Path, string User)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var user = request.Headers.GetValues("X-Niceda-User").Single();
            Requests.Add((request.RequestUri!.AbsolutePath, user));
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new[]
                {
                    new
                    {
                        name = user == "jane" ? "Jane record" : "Admin record",
                        salesNotes = "The full sales note stays available on hover.",
                    },
                }),
            });
        }
    }
}
