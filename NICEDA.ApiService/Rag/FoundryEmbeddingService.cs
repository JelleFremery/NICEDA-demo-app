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
        return (await RequestEmbeddingsAsync([text], cancellationToken))[0];
    }

    // One Foundry request per batch instead of one per chunk keeps the index
    // build fast, especially for Before mode's many fixed-size fragments.
    public async Task<IReadOnlyList<float[]>> EmbedManyAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(texts);
        var vectors = new List<float[]>(texts.Count);
        foreach (var batch in texts.Chunk(MaxBatchSize))
        {
            vectors.AddRange(await RequestEmbeddingsAsync(batch, cancellationToken));
        }
        return vectors;
    }

    internal const int MaxBatchSize = 64;

    private async Task<float[][]> RequestEmbeddingsAsync(string[] texts, CancellationToken cancellationToken)
    {
        foreach (var text in texts)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(text);
        }
        if (!IsAvailable)
        {
            throw new EmbeddingServiceUnavailableException(
                $"Azure AI Foundry embeddings are unavailable. Configure {EndpointConfigurationKey} and {ApiKeyConfigurationKey}.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            new Uri($"{_endpoint}/embeddings", UriKind.Absolute))
        {
            Content = JsonContent.Create(new { input = texts, model = _model }),
        };
        request.Headers.Add("api-key", _apiKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var payload = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(cancellationToken),
            cancellationToken: cancellationToken);

        var vectors = new float[texts.Length][];
        foreach (var item in payload.RootElement.GetProperty("data").EnumerateArray())
        {
            var index = item.TryGetProperty("index", out var indexElement) ? indexElement.GetInt32() : 0;
            if (index < 0 || index >= vectors.Length)
            {
                throw new InvalidOperationException("Azure AI Foundry returned an embedding for an unknown input.");
            }
            vectors[index] = [.. item.GetProperty("embedding").EnumerateArray().Select(value => value.GetSingle())];
        }

        if (vectors.Any(vector => vector is null || vector.Length == 0 || vector.Any(value => !float.IsFinite(value))))
        {
            throw new InvalidOperationException("Azure AI Foundry returned an invalid embedding vector.");
        }

        return vectors;
    }
}
