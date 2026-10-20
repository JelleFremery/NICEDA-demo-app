using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NICEDA.Web;
using NICEDA.Web.Components.Pages;

namespace NICEDA.Tests;

public sealed class TaskBoardTests
{
    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    public async Task BoardShowsPersistedTasksAndReloadsForIdentity(string mode)
    {
        var demo = new DemoState();
        demo.SetMode(mode);
        using var handler = new TaskHandler();
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://demo") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(demo);
        services.AddSingleton(new DataExplorerApiClient(client));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<TaskBoard>();
            var html = component.ToHtmlString();
            Assert.Contains("Open (1)", html);
            Assert.Contains("Completed (1)", html);
            Assert.Contains("Task #3009", html);
            Assert.Contains("Review Betabit renewal", html);
            Assert.Contains("2026-11-15", html);
            Assert.Contains("jane.okafor@niceda.example", html);
            Assert.DoesNotContain("Contoso follow-up", html);

            demo.SetMode(mode == "before" ? "after" : "before");
            Assert.Single(handler.Users);
            demo.SetUser("admin");
            Assert.Contains("Contoso follow-up", component.ToHtmlString());
            demo.SetUser("jane");
            Assert.DoesNotContain("Contoso follow-up", component.ToHtmlString());
            Assert.Equal(["jane", "admin", "jane"], handler.Users);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BoardShowsEmptyStateOrExplicitError(bool fail)
    {
        using var handler = new TaskHandler { Empty = true, Fail = fail };
        using var client = new HttpClient(handler) { BaseAddress = new Uri("http://demo") };
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new DemoState());
        services.AddSingleton(new DataExplorerApiClient(client));
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<TaskBoard>();
            var html = component.ToHtmlString();
            if (fail)
            {
                Assert.Contains("Could not load tasks.", html);
                Assert.Contains("Tasks unavailable", html);
                Assert.DoesNotContain("No open tasks.", html);
            }
            else
            {
                Assert.Contains("No open tasks.", html);
                Assert.Contains("No completed tasks.", html);
            }
        });
    }

    private sealed class TaskHandler : HttpMessageHandler
    {
        public List<string> Users { get; } = [];
        public bool Empty { get; init; }
        public bool Fail { get; init; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal("/api/data-explorer/sql/optimized/follow-up-tasks", request.RequestUri!.AbsolutePath);
            var user = request.Headers.GetValues("X-Niceda-User").Single();
            Users.Add(user);
            if (Fail)
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                {
                    Content = new StringContent("Tasks unavailable"),
                });
            var tasks = new List<FollowUpTask>();
            if (!Empty)
            {
                tasks.Add(new(3009, "betabit", "Review Betabit renewal", new(2026, 11, 15),
                    "jane.okafor@niceda.example", new(2026, 10, 20), false));
                tasks.Add(new(3008, "fenetre", "Renewal completed", new(2026, 10, 19),
                    "jane", new(2026, 10, 18), true));
                if (user == "admin")
                    tasks.Add(new(3010, "contoso", "Contoso follow-up", new(2026, 11, 16),
                        "admin", new(2026, 10, 20), false));
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(tasks) });
        }
    }
}
