using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NICEDA.Web;
using NICEDA.Web.Components;

namespace NICEDA.Tests;

public sealed class ChatPresentationTests
{
    [Fact]
    public async Task MarkdownAnswer_RendersBoldListsCodeAndTables()
    {
        var html = await RenderAsync("""
            **Customer overview**

            | Customer | Risk |
            | --- | --- |
            | Techorama | Low |

            - Evidence from `support-001`
            """);

        Assert.Contains("<strong>Customer overview</strong>", html);
        Assert.Contains("<table>", html);
        Assert.Contains("<th>Customer</th>", html);
        Assert.Contains("<td>Techorama</td>", html);
        Assert.Contains("<ul>", html);
        Assert.Contains("<code>support-001</code>", html);
        Assert.DoesNotContain("| --- |", html);
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("[click](javascript:alert%281%29)")]
    [InlineData("[click](data:text/html,malicious)")]
    [InlineData("![tracking](https://example.com/tracker.png)")]
    public async Task MarkdownAnswer_DoesNotRenderExecutableHtmlOrImages(string text)
    {
        var html = await RenderAsync(text);

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("<img", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("href=\"data:", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task RepeatedQuestions_AlwaysSendCurrentContextAndReadANewAnswer()
    {
        using var handler = new FreshAnswerHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://niceda.test") };
        var client = new DemoApiClient(http);

        foreach (var mode in new[] { "before", "after" })
        {
            foreach (var user in new[] { "jane", "admin" })
            {
                for (var repeat = 0; repeat < 2; repeat++)
                {
                    var response = await client.ChatAsync("Same question", mode, user, "same-action-key");
                    Assert.Equal($"Fresh answer {handler.Calls}", response["answer"]!.GetValue<string>());
                    Assert.Equal(mode, handler.LastRequest.GetProperty("mode").GetString());
                    Assert.Equal(user, handler.LastRequest.GetProperty("userId").GetString());
                    Assert.Equal("same-action-key", handler.LastRequest.GetProperty("requestIdempotencyKey").GetString());
                }
            }
        }

        Assert.Equal(8, handler.Calls);
    }

    [Fact]
    public async Task MarkdownAnswer_ReplacesPreviousAnswerWhenTextChanges()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        await renderer.Dispatcher.InvokeAsync(async () =>
        {
            AnswerHost? host = null;
            var component = await renderer.RenderComponentAsync<AnswerHost>(
                ParameterView.FromDictionary(new Dictionary<string, object?>
                {
                    ["Capture"] = (Action<AnswerHost>)(instance => host = instance),
                }));
            Assert.Contains("<strong>Before Jane</strong>", component.ToHtmlString());

            host!.Update("**After Admin**");

            var html = component.ToHtmlString();
            Assert.Contains("<strong>After Admin</strong>", html);
            Assert.DoesNotContain("Before Jane", html);
        });
    }

    private static async Task<string> RenderAsync(string text)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        await using var provider = services.BuildServiceProvider();
        await using var renderer = new HtmlRenderer(provider, provider.GetRequiredService<ILoggerFactory>());
        return await renderer.Dispatcher.InvokeAsync(async () =>
        {
            var component = await renderer.RenderComponentAsync<MarkdownAnswer>(
                ParameterView.FromDictionary(new Dictionary<string, object?> { ["Text"] = text }));
            return component.ToHtmlString();
        });
    }

    private sealed class FreshAnswerHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }
        public JsonElement LastRequest { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("/api/chat", request.RequestUri!.AbsolutePath);
            Assert.True(request.Headers.CacheControl?.NoCache);
            Assert.True(request.Headers.CacheControl?.NoStore);
            using var document = JsonDocument.Parse(await request.Content!.ReadAsStringAsync(cancellationToken));
            LastRequest = document.RootElement.Clone();
            Calls++;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($"{{\"answer\":\"Fresh answer {Calls}\"}}", Encoding.UTF8, "application/json"),
            };
        }
    }

    public sealed class AnswerHost : ComponentBase
    {
        private string _text = "**Before Jane**";

        [Parameter]
        public Action<AnswerHost>? Capture { get; set; }

        protected override void OnInitialized() => Capture?.Invoke(this);

        public void Update(string text)
        {
            _text = text;
            StateHasChanged();
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            builder.OpenComponent<MarkdownAnswer>(0);
            builder.AddComponentParameter(1, nameof(MarkdownAnswer.Text), _text);
            builder.CloseComponent();
        }
    }
}
