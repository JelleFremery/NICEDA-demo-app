using System.Net.Http.Json;
using System.Text.Json;

namespace NICEDA.ApiService.Rag;

public sealed class FoundryEmbeddingService : IEmbeddingService
{
    public const string EndpointConfigurationKey = "Foundry:Endpoint";
    public const string ApiKeyConfigurationKey = "Foundry:ApiKey";
    public const string ModelConfigurationKey = "Foundry:EmbeddingModel";

    private readonly HttpClient _httpClient;
    private readonly string? _endpoint;
    private readonly string? _apiKey;
    private readonly string _model;

    public FoundryEmbeddingService(HttpClient httpClient, IConfiguration configuration)
    {
        _httpClient = httpClient;
        _endpoint = configuration[EndpointConfigurationKey] is { } endpoint
            ? NormalizeEndpoint(endpoint)
            : null;
        _apiKey = configuration[ApiKeyConfigurationKey];
        _model = configuration[ModelConfigurationKey] ?? "text-embedding-3-small";
    }

    public static string NormalizeEndpoint(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
        var normalized = endpoint.Trim().TrimEnd('/');
        if (normalized.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase))
        {
            return normalized;
        }

        if (normalized.EndsWith("/openai", StringComparison.OrdinalIgnoreCase))
        {
            return $"{normalized}/v1";
        }

        return $"{normalized}/openai/v1";
    }

    public bool IsAvailable =>
        !string.IsNullOrWhiteSpace(_endpoint)
        && !string.IsNullOrWhiteSpace(_apiKey)
        && Uri.TryCreate(_endpoint, UriKind.Absolute, out _);

    public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        if (!IsAvailable)
        {
            throw new EmbeddingServiceUnavailableException(
                $"Azure AI Foundry embeddings are unavailable. Configure {EndpointConfigurationKey} and {ApiKeyConfigurationKey}.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"{_endpoint}/embeddings", UriKind.Absolute))
        {
            Content = JsonContent.Create(new { input = text, model = _model }),
        };
        request.Headers.Add("api-key", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var vector = payload.RootElement
            .GetProperty("data")[0]
            .GetProperty("embedding")
            .EnumerateArray()
            .Select(value => value.GetSingle())
            .ToArray();

        if (vector.Length == 0 || vector.Any(value => !float.IsFinite(value)))
        {
            throw new InvalidOperationException("Azure AI Foundry returned an invalid embedding vector.");
        }

        return vector;
    }
}
