namespace NICEDA.ApiService.Rag;

public interface IEmbeddingService
{
    bool IsAvailable { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);

    async Task<IReadOnlyList<float[]>> EmbedManyAsync(
        IReadOnlyList<string> texts, CancellationToken cancellationToken = default)
    {
        var vectors = new List<float[]>(texts.Count);
        foreach (var text in texts)
        {
            vectors.Add(await EmbedAsync(text, cancellationToken));
        }
        return vectors;
    }
}

public sealed class EmbeddingServiceUnavailableException(string message)
    : InvalidOperationException(message);
