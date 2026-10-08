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
        Assert.Equal("question", handler.Input);
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public Uri? RequestUri { get; private set; }
        public string? ApiKey { get; private set; }
        public string? Model { get; private set; }
        public string? Input { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestUri = request.RequestUri;
            ApiKey = request.Headers.GetValues("api-key").Single();
            var body = await request.Content!.ReadFromJsonAsync<EmbeddingRequest>(
                cancellationToken: cancellationToken);
            Model = body?.Model;
            Input = body?.Input;
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new
                {
                    data = new[] { new { embedding = new[] { 0.25f, 0.75f } } },
                }),
            };
        }
    }

    private sealed record EmbeddingRequest(string Model, string Input);
}
