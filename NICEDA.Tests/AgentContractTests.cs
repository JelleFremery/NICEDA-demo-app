using Microsoft.Extensions.Configuration;
using NICEDA.ApiService.Agent;
using NICEDA.ApiService.Feedback;
using NICEDA.ApiService.Rag;
using NICEDA.ApiService.Security;
using Microsoft.Extensions.DependencyInjection;

namespace NICEDA.Tests;

public sealed class AgentContractTests
{
    [Theory]
    [InlineData("Before", AgentMode.Before)]
    [InlineData("after", AgentMode.After)]
    public void ModeParser_AcceptsBeforeAndAfter(string value, AgentMode expected)
    {
        Assert.True(AgentModes.TryParse(value, out var actual));
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("raw")]
    [InlineData("")]
    public void ModeParser_RejectsUnsupportedModes(string value)
    {
        Assert.False(AgentModes.TryParse(value, out _));
    }

    [Fact]
    public void DemoIdentity_EnforcesCustomerScope()
    {
        var jane = DemoIdentity.Resolve("jane");

        Assert.True(jane.CanAccess("techorama", "EU"));
        Assert.False(jane.CanAccess("contoso", "NA"));
        Assert.True(DemoIdentity.Resolve("admin").CanAccess("contoso", "NA"));
    }

    [Fact]
    public void FoundryConfiguration_RequiresEndpointAndCredential()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        Assert.False(FoundryConfiguration.TryLoad(configuration, out _));
    }

    [Fact]
    public void FoundryConfiguration_NormalizesOpenAiCompatibleEndpointAndDefaults()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Foundry:Endpoint"] = "https://foundry.example",
                ["Foundry:ApiKey"] = "test-key",
            })
            .Build();

        Assert.True(FoundryConfiguration.TryLoad(configuration, out var settings));
        Assert.Equal(new Uri("https://foundry.example/openai/v1/"), settings!.Endpoint);
        Assert.Equal("gpt-6.1-sol", settings.ChatModel);
        Assert.Equal("text-embedding-3-small", settings.EmbeddingModel);
    }

    [Fact]
    public async Task ChatEndpoint_ReturnsServiceUnavailableWithoutFoundryCredentials()
    {
        var services = new ServiceCollection();
        using var provider = services.BuildServiceProvider();
        var knowledge = new KnowledgeSearchService(
            new UnusedKnowledgeSource(),
            new UnusedEmbeddingService());
        var agent = new AgentChatService(
            new ConfigurationBuilder().Build(),
            provider.GetRequiredService<IServiceScopeFactory>(),
            knowledge,
            new FeedbackService(new UnusedFeedbackStore()));

        var result = await AgentChatEndpoints.HandleAsync(
            new AgentChatRequest("After", "jane", "Summarize the account."),
            agent,
            CancellationToken.None);

        Assert.Equal(503, result.GetType().GetProperty("StatusCode")?.GetValue(result));
    }

    [Fact]
    public async Task FeedbackEndpoint_RejectsInvalidRating()
    {
        var agent = CreateAgent(new ConfigurationBuilder().Build());

        var result = await AgentChatEndpoints.HandleFeedbackAsync(
            new AgentFeedbackRequest("interaction-id", "jane", 0, null),
            agent,
            CancellationToken.None);

        Assert.Equal(400, result.GetType().GetProperty("StatusCode")?.GetValue(result));
    }

    [Fact]
    public async Task FeedbackEndpoint_RejectsUnknownInteractions()
    {
        var agent = CreateAgent(new ConfigurationBuilder().Build());

        var result = await AgentChatEndpoints.HandleFeedbackAsync(
            new AgentFeedbackRequest("interaction-id", "jane", 1, "Useful"),
            agent,
            CancellationToken.None);

        Assert.Equal(404, result.GetType().GetProperty("StatusCode")?.GetValue(result));
    }

    [Fact]
    public async Task Feedback_WritesOneRecordPerTouchedCustomerForTheOriginalUser()
    {
        var store = new CapturingFeedbackStore();
        var agent = CreateAgent(new ConfigurationBuilder().Build(), store);
        agent.TrackInteraction("abc", "jane", "After",
        [
            new AgentChatService.TouchedCustomer("betabit", ["search_knowledge"]),
            new AgentChatService.TouchedCustomer("techorama", ["create_follow_up_task", "get_customer_health"]),
        ]);

        var result = await AgentChatEndpoints.HandleFeedbackAsync(
            new AgentFeedbackRequest("abc", "jane", -1, "Wrong customer"), agent, CancellationToken.None);

        Assert.Equal(204, result.GetType().GetProperty("StatusCode")?.GetValue(result));
        Assert.Collection(store.Records,
            record =>
            {
                Assert.Equal("betabit", record.CustomerId);
                Assert.Equal("search_knowledge", record.ToolName);
                Assert.Equal(AgentChatService.RatingUseCase, record.UseCase);
                Assert.Equal("interaction:abc", record.Entity);
                Assert.Equal("not-helpful", record.ToolOutcome);
                Assert.Contains("Wrong customer", record.UserInteraction);
            },
            record =>
            {
                Assert.Equal("techorama", record.CustomerId);
                Assert.Equal("create_follow_up_task,get_customer_health", record.ToolName);
            });
        Assert.All(store.Records, record => Assert.Equal("jane", record.UserId));
        Assert.All(store.Records, record => Assert.Equal("After", record.Mode));
    }

    [Fact]
    public async Task Feedback_RejectsADifferentUserRatingSomeoneElsesInteraction()
    {
        var store = new CapturingFeedbackStore();
        var agent = CreateAgent(new ConfigurationBuilder().Build(), store);
        agent.TrackInteraction("abc", "jane", "After", [new AgentChatService.TouchedCustomer("betabit", ["search_knowledge"])]);

        var result = await AgentChatEndpoints.HandleFeedbackAsync(
            new AgentFeedbackRequest("abc", "admin", 1, null), agent, CancellationToken.None);

        Assert.Equal(403, result.GetType().GetProperty("StatusCode")?.GetValue(result));
        Assert.Empty(store.Records);
    }

    [Fact]
    public async Task Feedback_WithoutTouchedCustomers_WritesSingleUnscopedRecord()
    {
        var store = new CapturingFeedbackStore();
        var agent = CreateAgent(new ConfigurationBuilder().Build(), store);
        agent.TrackInteraction("abc", "admin", "Before", []);

        await AgentChatEndpoints.HandleFeedbackAsync(
            new AgentFeedbackRequest("abc", "admin", 1, null), agent, CancellationToken.None);

        var record = Assert.Single(store.Records);
        Assert.Null(record.CustomerId);
        Assert.Equal("helpful", record.ToolOutcome);
    }

    private sealed class CapturingFeedbackStore : IFeedbackStore
    {
        public List<FeedbackRecord> Records { get; } = [];

        public Task SaveAsync(FeedbackRecord record, CancellationToken cancellationToken = default)
        {
            Records.Add(record);
            return Task.CompletedTask;
        }

        public Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
    [Theory]
    [InlineData("http://localhost:5100", "http://localhost:5100/mcp")]
    [InlineData("http://localhost:5100/", "http://localhost:5100/mcp")]
    [InlineData("http://localhost:5100/mcp", "http://localhost:5100/mcp")]
    [InlineData("https://mcpserver:8443/mcp/", "https://mcpserver:8443/mcp/")]
    public void McpEndpoint_NormalizesBaseUrlToMcpPath(string configured, string expected)
    {
        Assert.True(AgentMcpContract.TryGetMcpEndpoint(configured, out var endpoint));
        Assert.Equal(new Uri(expected), endpoint);
    }

    [Theory]
    [InlineData("https://bblc-we-ai-foundry.services.ai.azure.com/", "https://bblc-we-ai-foundry.services.ai.azure.com/openai/v1/")]
    [InlineData("https://bblc-we-ai-foundry.services.ai.azure.com/openai/v1", "https://bblc-we-ai-foundry.services.ai.azure.com/openai/v1/")]
    public void FoundryEndpoint_NormalizesBaseResourceUrl(string configured, string expected)
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Foundry:Endpoint"] = configured,
            ["Foundry:ApiKey"] = "test-key",
        }).Build();

        Assert.True(FoundryConfiguration.TryLoad(configuration, out var settings));
        Assert.Equal(new Uri(expected), settings!.Endpoint);
        Assert.Equal("gpt-6.1-sol", settings.ChatModel);
    }
    private static AgentChatService CreateAgent(IConfiguration configuration, IFeedbackStore? store = null)
    {
        var services = new ServiceCollection();
        var provider = services.BuildServiceProvider();
        var knowledge = new KnowledgeSearchService(
            new UnusedKnowledgeSource(),
            new UnusedEmbeddingService());
        return new AgentChatService(
            configuration,
            provider.GetRequiredService<IServiceScopeFactory>(),
            knowledge,
            new FeedbackService(store ?? new UnusedFeedbackStore()));
    }

    [Fact]
    public void ChatRoutesMatchTheWebClientContract()
    {
        Assert.Equal("/api/chat", AgentMcpContract.ChatPath);
        Assert.Equal("/api/chat/feedback", AgentMcpContract.ChatFeedbackPath);
    }

    private sealed class UnusedKnowledgeSource : IKnowledgeDocumentSource
    {
        public Task<IReadOnlyList<KnowledgeDocument>> LoadAsync(
            string mode, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The offline chat test must not access knowledge.");
    }

    private sealed class UnusedEmbeddingService : IEmbeddingService
    {
        public bool IsAvailable => false;

        public Task<float[]> EmbedAsync(string text, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The offline chat test must not request embeddings.");
    }

    private sealed class UnusedFeedbackStore : IFeedbackStore
    {
        public Task SaveAsync(FeedbackRecord record, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The offline chat test must not write feedback.");

        public Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The offline chat test must not delete feedback.");
    }
}
