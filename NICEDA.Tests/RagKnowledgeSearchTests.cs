using NICEDA.ApiService.Rag;

namespace NICEDA.Tests;

public sealed class RagKnowledgeSearchTests
{
    private static readonly string[] collection = ["betabit", "fenetre", "techorama"];

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
            collection));
    }

    [Fact]
    public async Task SearchAsync_ExcludesUnauthorizedDocumentsBeforeEmbeddingAndRanking()
    {
        var embeddings = new FakeEmbeddingService();
        var service = CreateService(embeddings);

        var results = await service.SearchAsync("account risk", "after", "jane");

        Assert.DoesNotContain(results, result => result.CustomerId == "contoso");
        Assert.DoesNotContain("Contoso account risk.", embeddings.EmbeddedInputs);
        Assert.DoesNotContain("Fabrikam renewal at risk.", embeddings.EmbeddedInputs);
        Assert.DoesNotContain(embeddings.EmbeddedInputs, input =>
            input.Contains("Restricted", StringComparison.Ordinal)
            || input.Contains("Confidential", StringComparison.Ordinal)
            || input.Contains("renewal-risk-escalation", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SearchAsync_WarmAdminIndexIsNotReusedForJane()
    {
        var embeddings = new FakeEmbeddingService();
        var service = CreateService(embeddings);
        var admin = await service.SearchAsync("account risk", "after", "admin");
        Assert.Contains(admin, result => result.CustomerId == "contoso");
        embeddings.EmbeddedInputs.Clear();

        var jane = await service.SearchAsync("account risk", "After", "jane");
        Assert.DoesNotContain(jane, result => result.CustomerId is "contoso" or "fabrikam");
        Assert.Contains("Data export timed out.", embeddings.EmbeddedInputs);
        Assert.DoesNotContain("Contoso account risk.", embeddings.EmbeddedInputs);
        Assert.DoesNotContain("Fabrikam renewal at risk.", embeddings.EmbeddedInputs);

        embeddings.EmbeddedInputs.Clear();
        _ = await service.SearchAsync("account risk", "after", "jane.okafor@niceda.example");
        Assert.Equal(["account risk"], embeddings.EmbeddedInputs);
    }

    [Fact]
    public async Task SearchAsync_NoAuthorizedDocumentsDoesNotEmbedEvenTheQuestion()
    {
        var embeddings = new FakeEmbeddingService();
        var service = new KnowledgeSearchService(new FakeKnowledgeDocumentSource(
        [
            new("contoso", "contoso", "NA", "Restricted", "risk", "Contoso account risk."),
            new("wrong-region", "techorama", "NA", "Restricted", "risk", "Out-of-region information.")
        ]), embeddings);

        Assert.Empty(await service.SearchAsync("account risk", "after", "jane"));
        Assert.Empty(embeddings.EmbeddedInputs);
    }

    [Theory]
    [InlineData("before")]
    [InlineData("after")]
    public async Task InvalidateCustomerAsync_RemovesFromEveryPermissionScope(string mode)
    {
        var service = CreateService(new FakeEmbeddingService());
        _ = await service.SearchAsync("data export", mode, "admin");
        _ = await service.SearchAsync("data export", mode, "jane");
        await service.InvalidateCustomerAsync(" Techorama ");

        Assert.DoesNotContain(await service.SearchAsync("data export", mode, "admin"),
            result => result.CustomerId == "techorama");
        Assert.DoesNotContain(await service.SearchAsync("data export", mode, "jane"),
            result => result.CustomerId == "techorama");
    }

    [Fact]
    public async Task InvalidateCustomerAsync_DuringIndexBuildCannotResurrectDocument()
    {
        var embeddings = new BlockingEmbeddingService("Data export timed out.");
        var service = new KnowledgeSearchService(new FakeKnowledgeDocumentSource(
        [
            new("techorama-export", "techorama", "EU", "Confidential", "incident", "Data export timed out.")
        ]), embeddings);
        var search = service.SearchAsync("data export", "after", "jane");
        await embeddings.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var deletion = service.InvalidateCustomerAsync("techorama");
        embeddings.Release.SetResult();

        Assert.Empty(await search);
        await deletion;
        Assert.Empty(await service.SearchAsync("data export", "after", "admin"));
    }

    [Fact]
    public async Task InvalidateCustomerAsync_DuringQueryEmbeddingExcludesSnapshotDocument()
    {
        var embeddings = new BlockingEmbeddingService("query");
        var service = new KnowledgeSearchService(new FakeKnowledgeDocumentSource(
        [
            new("techorama-export", "techorama", "EU", "Confidential", "incident", "Data export timed out.")
        ]), embeddings);
        var search = service.SearchAsync("query", "after", "jane");
        await embeddings.Started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await service.InvalidateCustomerAsync("techorama");
        embeddings.Release.SetResult();
        Assert.Empty(await search);
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
    public async Task SearchAsync_BeforeReturnsFewerFixedSizeFragmentsWithoutConcepts()
    {
        var sentence = "Security review found an export gap; mitigation is owner-approved encryption. ";
        var content = string.Concat(Enumerable.Repeat(sentence, 8));
        var source = new FakeKnowledgeDocumentSource(
        [
            new("support-export", "techorama", "EU", "Confidential", "security-review", content),
            new("support-other", "betabit", "EU", "Confidential", "incident-resolution", content),
        ]);
        var service = new KnowledgeSearchService(source, new FakeEmbeddingService());

        var before = await service.SearchAsync("export", "before", "jane");
        var after = await service.SearchAsync("export", "after", "jane");

        Assert.Equal(KnowledgeSearchService.BeforeResultCount, before.Count);
        Assert.All(before, result =>
        {
            Assert.Null(result.Concept);
            Assert.True(result.Content.Length <= KnowledgeSearchService.BeforeFragmentLength);
            Assert.NotEqual(content.Trim(), result.Content);
        });
        Assert.Equal(2, after.Count);
        Assert.All(after, result =>
        {
            Assert.NotNull(result.Concept);
            Assert.Equal(content, result.Content);
        });
    }

    // NICEDA-PRACTICE-9: the stage question, against the real seed documents.
    [Fact]
    public async Task StageQuestion_ContosoSecurityReview_RealSeed()
    {
        const string question = "Summarize Contoso's security review with sources.";
        var source = new SeedKnowledgeSource();

        var adminAfter = await new KnowledgeSearchService(source, new KeywordEmbeddingService())
            .SearchAsync(question, "after", "admin");
        Assert.Equal(
            ["security-finding-access-controls", "security-finding-data-residency", "security-finding-incident-response"],
            adminAfter.Take(3).Select(result => result.Concept).Order(StringComparer.Ordinal));
        Assert.Contains(adminAfter, result => result.Id == "optimized-sup-005c"
            && result.Content.Contains("dedicated Contoso escalation contact", StringComparison.Ordinal));

        var adminBefore = await new KnowledgeSearchService(source, new KeywordEmbeddingService())
            .SearchAsync(question, "before", "admin");
        Assert.Equal(KnowledgeSearchService.BeforeResultCount, adminBefore.Count);
        Assert.All(adminBefore, result =>
        {
            Assert.Null(result.Concept);
            Assert.True(result.Content.Length <= KnowledgeSearchService.BeforeFragmentLength);
        });

        foreach (var mode in new[] { "before", "after" })
        {
            var embeddings = new KeywordEmbeddingService();
            var jane = await new KnowledgeSearchService(source, embeddings).SearchAsync(question, mode, "jane");
            Assert.DoesNotContain(jane, result => result.CustomerId is "contoso" or "fabrikam");
            Assert.DoesNotContain(embeddings.EmbeddedInputs, input =>
                input.Contains("Contoso", StringComparison.Ordinal) && input != question);
        }
    }

    [Fact]
    public async Task SearchAsync_BeforeFragmentsAreAuthorizedBeforeEmbedding()
    {
        var embeddings = new FakeEmbeddingService();
        var service = CreateService(embeddings);

        var results = await service.SearchAsync("account risk", "before", "jane");

        Assert.DoesNotContain(results, result => result.CustomerId is "contoso" or "fabrikam");
        Assert.DoesNotContain(embeddings.EmbeddedInputs, input =>
            input.Contains("Contoso", StringComparison.Ordinal)
            || input.Contains("Fabrikam", StringComparison.Ordinal));
    }

    [Fact]
    public void ExtractContent_ExcludesCosmosSystemProperties()
    {
        var document = new System.Text.Json.Nodes.JsonObject
        {
            ["id"] = "support-001",
            ["note"] = "Export timed out.",
            ["_rid"] = "AQAAAIIAAAA=",
            ["_etag"] = "d824aa72",
            ["_attachments"] = "attachments/",
        };

        Assert.Equal("Export timed out.", CosmosKnowledgeDocumentSource.ExtractContent(document));
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
            new KnowledgeDocument("fabrikam-risk", "fabrikam", "NA", "Restricted", "renewal-risk-escalation", "Fabrikam renewal at risk."),
        ]);
        return new KnowledgeSearchService(source, embeddings);
    }

    private sealed class SeedKnowledgeSource : IKnowledgeDocumentSource
    {
        public Task<IReadOnlyList<KnowledgeDocument>> LoadAsync(
            string mode, CancellationToken cancellationToken = default)
        {
            var (container, seed) = mode.Equals("before", StringComparison.OrdinalIgnoreCase)
                ? ("raw-support", NICEDA.ApiService.SupportDocs.RawSupportSeeder.SeedDocuments())
                : ("optimized-support", NICEDA.ApiService.SupportDocs.OptimizedSupportSeeder.SeedDocuments());
            IReadOnlyList<KnowledgeDocument> documents = [.. seed
                .Select(document => CosmosKnowledgeDocumentSource.ToKnowledgeDocument(document, container))
                .OfType<KnowledgeDocument>()];
            return Task.FromResult(documents);
        }
    }

    // Deterministic bag-of-words stand-in for the embedding model.
    private sealed class KeywordEmbeddingService : IEmbeddingService
    {
        private static readonly string[] Vocabulary =
            ["contoso", "security", "review", "access", "residency", "incident", "escalation",
             "renewal", "risk", "export", "sso", "billing", "reporting"];

        public List<string> EmbeddedInputs { get; } = [];

        public bool IsAvailable => true;

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            EmbeddedInputs.Add(text);
            var words = text.ToLowerInvariant()
                .Split([' ', '.', ',', ':', ';', '\'', '-', '—', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
                .ToHashSet();
            return Task.FromResult(Vocabulary.Select(word => words.Contains(word) ? 1f : 0f).ToArray());
        }
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
                ? [0, 1]
                : text.Contains("risk", StringComparison.OrdinalIgnoreCase)
                    ? [1, 0]
                    : new float[] { 0.5f, 0.5f };
            return Task.FromResult(vector);
        }

    }

    private sealed class BlockingEmbeddingService(string blockedText) : IEmbeddingService
    {
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsAvailable => true;

        public async Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default)
        {
            if (text == blockedText)
            {
                Started.TrySetResult();
                await Release.Task.WaitAsync(cancellationToken);
            }
            return [0, 1];
        }
    }
}
