namespace NICEDA.ApiService.Rag;

public interface IEmbeddingService
{
    bool IsAvailable { get; }

    Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default);
}

public sealed class EmbeddingServiceUnavailableException(string message)
    : InvalidOperationException(message);
