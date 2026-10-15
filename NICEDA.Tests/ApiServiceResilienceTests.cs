using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using NICEDA.ApiService.Rag;

namespace NICEDA.Tests;

public sealed class ApiServiceResilienceTests
{
    [Fact]
    public void EmbeddingClient_DoesNotUseTenSecondDefaultTimeout()
    {
        using var host = CreateHost(primaryHandler: null);
        var policy = host.Services.GetRequiredService<IOptionsMonitor<HttpStandardResilienceOptions>>()
            .Get($"{nameof(FoundryEmbeddingService)}-standard");
        using var client = host.Services.GetRequiredService<IHttpClientFactory>()
            .CreateClient(nameof(FoundryEmbeddingService));

        Assert.Equal(TimeSpan.FromSeconds(90), policy.AttemptTimeout.Timeout);
        Assert.Equal(TimeSpan.FromMinutes(4), policy.TotalRequestTimeout.Timeout);
        Assert.Equal(Timeout.InfiniteTimeSpan, client.Timeout);
    }

    [Fact]
    public async Task Embedding_SucceedsBeyondDefaultAttemptTimeout()
    {
        using var handler = new SlowEmbeddingHandler(TimeSpan.FromSeconds(11));
        using var host = CreateHost(handler);
        var embeddings = host.Services.GetRequiredService<IEmbeddingService>();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));

        var vector = await embeddings.EmbedAsync("question", deadline.Token);

        Assert.Equal(new[] { 1f, 0f }, vector);
        Assert.Equal(1, handler.Calls);
    }

    private static IHost CreateHost(HttpMessageHandler? primaryHandler)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[FoundryEmbeddingService.EndpointConfigurationKey] = "https://foundry.example/";
        builder.Configuration[FoundryEmbeddingService.ApiKeyConfigurationKey] = "test-key";
        // Mirrors NICEDA.ApiService/Program.cs.
        builder.AddServiceDefaults(addStandardResilienceHandler: false);
        builder.Services.AddNicedaRagAndFeedback();
        if (primaryHandler is not null)
        {
            builder.Services.AddHttpClient(nameof(FoundryEmbeddingService))
                .ConfigurePrimaryHttpMessageHandler(() => primaryHandler);
        }
        return builder.Build();
    }

    private sealed class SlowEmbeddingHandler(TimeSpan delay) : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            await Task.Delay(delay, cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    data = new[] { new { index = 0, embedding = new[] { 1f, 0f } } },
                }),
            };
        }
    }
}
