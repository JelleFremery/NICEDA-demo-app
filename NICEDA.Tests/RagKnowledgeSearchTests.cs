using NICEDA.ApiService.Rag;

namespace NICEDA.Tests;

public sealed class RagKnowledgeSearchTests
{
    [Fact]
    public async Task SearchAsync_ReturnsExpectedMatchingChunkForJane()
    {
        var embeddings = new FakeEmbeddingService();
        var service = CreateService(embeddings);

        var results = await service.SearchAsync("data export", "after", "jane");

        Assert.NotEmpty(results);
        Assert.Equal("techorama-export", results[0].Id);
        Assert.All(results, result => Assert.Contains(
            result.CustomerId,
            new[] { "betabit", "fenetre", "techorama" }));
    }

    [Fact]
    public async Task SearchAsync_ExcludesUnauthorizedDocumentsBeforeQueryRanking()
    {
        var embeddings = new FakeEmbeddingService();
        var service = CreateService(embeddings);

        var results = await service.SearchAsync("account risk", "after", "jane");

        Assert.DoesNotContain(results, result => result.CustomerId == "contoso");
        Assert.Contains("Contoso account risk.", embeddings.EmbeddedInputs);
    }

    [Fact]
    public async Task SearchAsync_AdminCanRetrieveAllCustomers()
    {
        var service = CreateService(new FakeEmbeddingService());

        var results = await service.SearchAsync("account risk", "after", "admin");

        Assert.Contains(results, result => result.CustomerId == "contoso");
    }

    [Fact]
    public async Task InvalidateCustomerAsync_RemovesCustomerFromCachedIndexes()
    {
        var service = CreateService(new FakeEmbeddingService());
        _ = await service.SearchAsync("data export", "after", "admin");

        await service.InvalidateCustomerAsync("techorama");
        var results = await service.SearchAsync("data export", "after", "admin");

        Assert.DoesNotContain(results, result => result.CustomerId == "techorama");
    }

    [Fact]
    public async Task RemoveCustomer_RemovesCustomerFromBothCachedIndexes()
    {
        var service = CreateService(new FakeEmbeddingService());
        _ = await service.SearchAsync("data export", "before", "admin");
        _ = await service.SearchAsync("data export", "after", "admin");

        service.RemoveCustomer("techorama");

        var before = await service.SearchAsync("data export", "before", "admin");
        var after = await service.SearchAsync("data export", "after", "admin");
        Assert.DoesNotContain(before, result => result.CustomerId == "techorama");
        Assert.DoesNotContain(after, result => result.CustomerId == "techorama");
    }

    [Fact]
    public async Task RemoveCustomer_BeforeFirstIndexBuildExcludesCustomerInBothModes()
    {
        var embeddings = new FakeEmbeddingService();
        var service = CreateService(embeddings);

        service.RemoveCustomer("techorama");

        var before = await service.SearchAsync("data export", "before", "admin");
        var after = await service.SearchAsync("data export", "after", "admin");
        Assert.DoesNotContain(before, result => result.CustomerId == "techorama");
        Assert.DoesNotContain(after, result => result.CustomerId == "techorama");
        Assert.DoesNotContain("Data export timed out.", embeddings.EmbeddedInputs);
    }

    [Fact]
    public async Task SearchAsync_RejectsUnknownUser()
    {
        var service = CreateService(new FakeEmbeddingService());

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => service.SearchAsync("data export", "after", "unknown"));
    }

    private static KnowledgeSearchService CreateService(FakeEmbeddingService embeddings)
    {
        var source = new FakeKnowledgeDocumentSource(
        [
            new KnowledgeDocument("techorama-export", "techorama", "EU", "Confidential", "incident-resolution", "Data export timed out."),
            new KnowledgeDocument("betabit-risk", "betabit", "EU", "Confidential", "renewal-risk-escalation", "Betabit renewal at risk."),
            new KnowledgeDocument("contoso-risk", "contoso", "NA", "Restricted", "renewal-risk-escalation", "Contoso account risk."),
            new KnowledgeDocument("fenetre-notes", "fenetre", "EU", "Confidential", "meeting-notes", "Fenetre reporting discussion."),
        ]);
        return new KnowledgeSearchService(source, embeddings);
    }

    private sealed class FakeKnowledgeDocumentSource(IReadOnlyList<KnowledgeDocument> documents)
        : IKnowledgeDocumentSource
    {
        public Task<IReadOnlyList<KnowledgeDocument>> LoadAsync(
            string mode, CancellationToken cancellationToken = default) =>
            Task.FromResult(documents);
    }

    private sealed class FakeEmbeddingService : IEmbeddingService
    {
        public List<string> EmbeddedInputs { get; } = [];

        public bool IsAvailable => true;

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            EmbeddedInputs.Add(text);
            var vector = text.Contains("export", StringComparison.OrdinalIgnoreCase)
                ? new float[] { 0, 1 }
                : text.Contains("risk", StringComparison.OrdinalIgnoreCase)
                    ? new float[] { 1, 0 }
                    : new float[] { 0.5f, 0.5f };
            return Task.FromResult(vector);
        }
    }
}
