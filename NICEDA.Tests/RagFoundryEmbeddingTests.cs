using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;
using NICEDA.ApiService.Rag;

namespace NICEDA.Tests;

public sealed class RagFoundryEmbeddingTests
{
    [Fact]
    public async Task EmbedAsync_ReportsUnavailableWhenCredentialsAreMissing()
    {
        var configuration = new ConfigurationBuilder().Build();
        var service = new FoundryEmbeddingService(new HttpClient(), configuration);

        Assert.False(service.IsAvailable);
        await Assert.ThrowsAsync<EmbeddingServiceUnavailableException>(
            () => service.EmbedAsync("test question"));
    }

    [Theory]
    [InlineData("https://bblc-we-ai-foundry.services.ai.azure.com/", "https://bblc-we-ai-foundry.services.ai.azure.com/openai/v1")]
    [InlineData("https://foundry.example/openai/v1/", "https://foundry.example/openai/v1")]
    [InlineData("https://foundry.example/openai", "https://foundry.example/openai/v1")]
    public async Task EmbedAsync_NormalizesEndpointAndCallsOpenAiCompatibleRoute(
        string endpoint, string expectedBaseEndpoint)
    {
        var handler = new CapturingHandler();
        var httpClient = new HttpClient(handler);
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [FoundryEmbeddingService.EndpointConfigurationKey] = endpoint,
                [FoundryEmbeddingService.ApiKeyConfigurationKey] = "test-key",
            })
            .Build();
        var service = new FoundryEmbeddingService(httpClient, configuration);

        var vector = await service.EmbedAsync("question");

        Assert.Equal(new[] { 0.25f, 0.75f }, vector);
        Assert.Equal($"{expectedBaseEndpoint}/embeddings", handler.RequestUri?.AbsoluteUri);
        Assert.Equal("test-key", handler.ApiKey);
        Assert.Equal("text-embedding-3-small", handler.Model);
        Assert.Equal(new[] { "question" }, handler.Inputs);
    }

    [Fact]
    public async Task EmbedManyAsync_BatchesRequestsAndOrdersVectorsByIndex()
    {
        var handler = new CapturingHandler { ReverseResponseOrder = true };
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [FoundryEmbeddingService.EndpointConfigurationKey] = "https://foundry.example/",
                [FoundryEmbeddingService.ApiKeyConfigurationKey] = "test-key",
            })
            .Build();
        var service = new FoundryEmbeddingService(new HttpClient(handler), configuration);
        var texts = Enumerable.Range(0, FoundryEmbeddingService.MaxBatchSize + 3)
            .Select(i => $"chunk {i}")
            .ToArray();

        var vectors = await service.EmbedManyAsync(texts);

        Assert.Equal(2, handler.RequestCount);
        Assert.Equal(texts.Length, vectors.Count);
        Assert.Equal(0f, vectors[0][0]);
        Assert.Equal(FoundryEmbeddingService.MaxBatchSize + 2, vectors[^1][0]);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        private int _offset;

        public bool ReverseResponseOrder { get; init; }
        public int RequestCount { get; private set; }
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Model { get; private set; }
        public string[]? Inputs { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("api-key").Single();
            var body = await request.Content!.ReadFromJsonAsync<EmbeddingRequest>(
                cancellationToken: cancellationToken);
            Model = body?.Model;
            Inputs = body?.Input;
            var count = Inputs?.Length ?? 0;
            var data = Enumerable.Range(0, count)
                .Select(i => new
                {
                    index = i,
                    embedding = ReverseResponseOrder ? new[] { (float)(_offset + i), 1f } : new[] { 0.25f, 0.75f },
                })
                .ToArray();
            _offset += count;
            if (ReverseResponseOrder)
            {
                Array.Reverse(data);
            }
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { data }),
            };
        }
    }

    private sealed record EmbeddingRequest(string Model, string[] Input);
}
