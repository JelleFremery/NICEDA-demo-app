using System.Net;
using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NICEDA.Web;

namespace NICEDA.Tests;

public sealed class AgentChatResilienceTests
{
    [Fact]
    public void ChatClient_HasFiveMinuteBudgetWithoutChangingExplorerPolicy()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults(addStandardResilienceHandler: false);
        builder.Services.AddDemoApiClient();
        builder.Services.AddHttpClient<DataExplorerApiClient>()
            .AddStandardResilienceHandler(options => options.Retry.DisableForUnsafeHttpMethods());
        using var host = builder.Build();
        var factory = host.Services.GetRequiredService<IHttpClientFactory>();
        using var chat = factory.CreateClient(nameof(DemoApiClient));
        using var explorer = factory.CreateClient(nameof(DataExplorerApiClient));
        var options = host.Services.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>();
        var chatPolicy = options.Get($"{nameof(DemoApiClient)}-standard");
        var explorerPolicy = options.Get($"{nameof(DataExplorerApiClient)}-standard");

        Assert.Equal(TimeSpan.FromMinutes(5), chat.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(5), chatPolicy.TotalRequestTimeout.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(5), chatPolicy.AttemptTimeout.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(10), chatPolicy.CircuitBreaker.SamplingDuration);
        Assert.Equal(TimeSpan.FromSeconds(30), explorerPolicy.TotalRequestTimeout.Timeout);
        Assert.Equal(TimeSpan.FromSeconds(10), explorerPolicy.AttemptTimeout.Timeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, explorer.Timeout);
    }

    [Fact]
    public async Task Chat_SucceedsBeyondDefaultAttemptTimeout()
    {
        using var handler = new StubHandler(async cancellationToken =>
        {
            await Task.Delay(TimeSpan.FromSeconds(11), cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"answer\":\"Completed\"}", Encoding.UTF8, "application/json"),
            };
        });
        using var host = CreateHost(handler);
        var client = host.Services.GetRequiredService<DemoApiClient>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));

        var result = await client.ChatAsync("Question", "After", "jane", "same-key", deadline.Token);

        Assert.Equal("Completed", result["answer"]!.GetValue<string>());
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ChatAndFeedback_DoNotRetryFailedPosts(bool feedback)
    {
        using var handler = new StubHandler(_ =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)));
        using var host = CreateHost(handler);
        var client = host.Services.GetRequiredService<DemoApiClient>();

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            if (feedback)
                await client.FeedbackAsync("interaction", "jane", 1, "Helpful");
            else
                await client.ChatAsync("Question", "After", "jane", "same-key");
        });

        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Chat_StillHonorsCallerCancellation()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new StubHandler(async cancellationToken =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        });
        using var host = CreateHost(handler);
        var client = host.Services.GetRequiredService<DemoApiClient>();
        using var cancellation = new CancellationTokenSource();

        var request = client.ChatAsync("Question", "After", "jane", "same-key", cancellation.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => request);
        Assert.Equal(1, handler.Calls);
    }

    private static IHost CreateHost(HttpMessageHandler handler)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.AddServiceDefaults(addStandardResilienceHandler: false);
        builder.Services.AddDemoApiClient()
            .ConfigureHttpClient(client => client.BaseAddress = new Uri("http://localhost"))
            .ConfigurePrimaryHttpMessageHandler(() => handler);
        return builder.Build();
    }

    private sealed class StubHandler(Func<CancellationToken, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(cancellationToken);
        }
    }
}
