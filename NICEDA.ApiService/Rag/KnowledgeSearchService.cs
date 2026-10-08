using System.Collections.Concurrent;
using NICEDA.ApiService.Security;

namespace NICEDA.ApiService.Rag;

public sealed class KnowledgeSearchService(
    IKnowledgeDocumentSource documentSource,
    IEmbeddingService embeddingService)
{
    private readonly SemaphoreSlim _indexLock = new(1, 1);
    private readonly Dictionary<(string Mode, string Scope), List<IndexedDocument>> _indexes = [];

    // NICEDA-PRACTICE-6: in-process tombstones stop a concurrent or later lazy
    // index build from resurrecting a deleted customer's documents.
    private readonly ConcurrentDictionary<string, byte> _deletedCustomerIds =
        new(StringComparer.OrdinalIgnoreCase);

    public async Task<IReadOnlyList<KnowledgeSearchResult>> SearchAsync(
        string question,
        string mode,
        string userId,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(question);
        var identity = DemoIdentity.Resolve(userId);
        var index = await GetOrBuildIndexAsync(mode, identity, cancellationToken);
        var authorizedCandidates = index
            .Where(entry => !IsDeleted(entry.Document.CustomerId)
                && identity.CanAccess(entry.Document.CustomerId, entry.Document.Region))
            .ToArray();

        if (authorizedCandidates.Length == 0)
        {
            return [];
        }

        var queryVector = await embeddingService.EmbedAsync(question, cancellationToken);
        return authorizedCandidates
            .Where(entry => !IsDeleted(entry.Document.CustomerId))
            .Select(entry => new KnowledgeSearchResult(
                entry.Document.Id,
                entry.Document.CustomerId,
                entry.Document.Concept,
                entry.Document.Content,
                CosineSimilarity(queryVector, entry.Vector)))
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Id, StringComparer.Ordinal)
            .Take(5)
            .ToArray();
    }

    public async Task InvalidateCustomerAsync(
        string customerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        _deletedCustomerIds.TryAdd(customerId.Trim(), 0);
        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            foreach (var index in _indexes.Values)
            {
                index.RemoveAll(entry =>
                    string.Equals(entry.Document.CustomerId, customerId.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            _indexLock.Release();
        }
    }

    public void RemoveCustomer(string customerId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        _deletedCustomerIds.TryAdd(customerId.Trim(), 0);
        _indexLock.Wait();
        try
        {
            foreach (var index in _indexes.Values)
            {
                index.RemoveAll(entry =>
                    string.Equals(entry.Document.CustomerId, customerId.Trim(), StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private async Task<IReadOnlyList<IndexedDocument>> GetOrBuildIndexAsync(
        string mode, DemoUser identity, CancellationToken cancellationToken)
    {
        if (!string.Equals(mode, "before", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(mode, "after", StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException("Mode must be 'before' or 'after'.", nameof(mode));
        }

        // The demo has exactly two immutable permission scopes. Identity aliases
        // share a scope, but admin's cached corpus is never reused for Jane.
        var cacheKey = (mode.ToLowerInvariant(), identity.IsAdministrator ? "admin" : "jane");
        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_indexes.TryGetValue(cacheKey, out var cached))
            {
                return cached.ToArray();
            }

            var documents = await documentSource.LoadAsync(mode, cancellationToken);
            var index = new List<IndexedDocument>(documents.Count);
            foreach (var document in documents.Where(document => !IsDeleted(document.CustomerId)
                && identity.CanAccess(document.CustomerId, document.Region)))
            {
                var vector = await embeddingService.EmbedAsync(document.Content, cancellationToken);
                if (!IsDeleted(document.CustomerId))
                {
                    index.Add(new IndexedDocument(document, vector));
                }
            }

            _indexes[cacheKey] = index;
            return index.ToArray();
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private bool IsDeleted(string customerId) => _deletedCustomerIds.ContainsKey(customerId.Trim());

    private static double CosineSimilarity(IReadOnlyList<float> left, IReadOnlyList<float> right)
    {
        if (left.Count != right.Count || left.Count == 0)
        {
            throw new InvalidOperationException("Embedding vectors have incompatible dimensions.");
        }

        double dotProduct = 0;
        double leftMagnitude = 0;
        double rightMagnitude = 0;
        for (var i = 0; i < left.Count; i++)
        {
            dotProduct += left[i] * right[i];
            leftMagnitude += left[i] * left[i];
            rightMagnitude += right[i] * right[i];
        }

        if (leftMagnitude == 0 || rightMagnitude == 0)
        {
            return 0;
        }

        return dotProduct / (Math.Sqrt(leftMagnitude) * Math.Sqrt(rightMagnitude));
    }

    private sealed record IndexedDocument(KnowledgeDocument Document, float[] Vector);
}
