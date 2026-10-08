using System.Collections.Concurrent;
using System.Text.Json;
using NICEDA.ApiService.Feedback;
using NICEDA.ApiService.Rag;
using NICEDA.ApiService.Security;
using Microsoft.Agents.AI;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.AI;
using ModelContextProtocol.Client;
using OpenAI;
using OpenAI.Chat;

namespace NICEDA.ApiService.Agent;

public sealed class AgentChatService(
    IConfiguration configuration,
    IServiceScopeFactory scopeFactory,
    KnowledgeSearchService knowledgeSearch,
    FeedbackService feedbackService)
{
    private readonly ConcurrentDictionary<string, ChatInteraction> _interactions = new(StringComparer.Ordinal);

    public async Task<AgentChatResponse> ChatAsync(AgentChatRequest request, CancellationToken cancellationToken)
    {
        if (!FoundryConfiguration.TryLoad(configuration, out var foundry))
        {
            throw new FoundryNotConfiguredException();
        }

        if (!AgentModes.TryParse(request.Mode, out var mode))
        {
            throw new ArgumentException("Mode must be Before or After.", nameof(request));
        }

        var identity = DemoIdentity.Resolve(request.UserId);
        if (!AgentMcpContract.TryGetMcpEndpoint(configuration["Mcp:Endpoint"], out var mcpEndpoint))
        {
            throw new InvalidOperationException("Mcp:Endpoint must be configured with the MCP server base URL.");
        }

        await using var mcpClient = await McpClient.CreateAsync(new HttpClientTransport(new HttpClientTransportOptions
        {
            Endpoint = mcpEndpoint,
            Name = "NICEDA MCP server",
            AdditionalHeaders = new Dictionary<string, string>
            {
                [AgentMcpContract.UserHeader] = identity.UserId,
                [AgentMcpContract.ModeHeader] = mode.ToString(),
            },
        }), cancellationToken: cancellationToken);

        var mcpTools = await mcpClient.ListToolsAsync(cancellationToken: cancellationToken);
        var mcpTool = mcpTools.SingleOrDefault(tool => tool.Name == AgentMcpContract.ToolName);
        if (mcpTool is null || mcpTools.Count != 1)
        {
            throw new InvalidOperationException("The NICEDA MCP server must expose exactly create_follow_up_task.");
        }

        var citations = new ConcurrentQueue<KnowledgeCitation>();
        var traces = new ConcurrentQueue<AgentToolTrace>();
        var touchedCustomers = new ConcurrentDictionary<string, ConcurrentDictionary<string, byte>>(StringComparer.OrdinalIgnoreCase);

        void Touch(string? customerId, string source)
        {
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                touchedCustomers.GetOrAdd(customerId, _ => new(StringComparer.Ordinal)).TryAdd(source, 0);
            }
        }

        async Task<object> SearchKnowledgeAsync(string question, CancellationToken ct)
        {
            traces.Enqueue(new AgentToolTrace("search_knowledge", "started", null));
            var results = await knowledgeSearch.SearchAsync(question, mode.ToString(), identity.UserId, ct);
            foreach (var result in results)
            {
                citations.Enqueue(new KnowledgeCitation(
                    result.Id,
                    result.CustomerId,
                    result.Concept,
                    result.Content.Length <= 500 ? result.Content : result.Content[..500]));
                Touch(result.CustomerId, AgentMcpContract.SearchKnowledgeToolName);
            }
            traces.Enqueue(new AgentToolTrace("search_knowledge", "completed", $"{results.Count} citations"));
            return results;
        }

        async Task<object> GetCustomerHealthAsync(string? customerId, CancellationToken ct)
        {
            traces.Enqueue(new AgentToolTrace("get_customer_health", "started", customerId));
            if (!string.IsNullOrWhiteSpace(customerId) && !identity.CanAccess(customerId, null))
            {
                traces.Enqueue(new AgentToolTrace("get_customer_health", "denied", customerId));
                return new { error = "The selected identity is not authorized for that customer." };
            }

            using var scope = scopeFactory.CreateScope();
            var connection = scope.ServiceProvider.GetRequiredService<SqlConnection>();
            var rows = await Sql.CustomerHealthQueries.GetCustomerHealthAsync(connection, customerId, ct);
            var allowedRows = rows.Where(row => identity.CanAccess(row.CustomerId, row.Region)).ToArray();
            foreach (var row in allowedRows)
            {
                Touch(row.CustomerId, AgentMcpContract.GetCustomerHealthToolName);
            }
            traces.Enqueue(new AgentToolTrace("get_customer_health", "completed", $"{allowedRows.Length} customer records"));
            return allowedRows;
        }

        async Task<object?> CreateFollowUpTaskAsync(
            string customerId, string reason, DateOnly dueDate, CancellationToken ct)
        {
            traces.Enqueue(new AgentToolTrace(AgentMcpContract.ToolName, "started", customerId));
            var result = await mcpTool.InvokeAsync(
                new AIFunctionArguments
                {
                    ["customerId"] = customerId,
                    ["reason"] = reason,
                    ["dueDate"] = dueDate,
                },
                ct);
            Touch(customerId, AgentMcpContract.ToolName);
            traces.Enqueue(new AgentToolTrace(AgentMcpContract.ToolName, "completed", customerId));
            return result;
        }

        var tools = new List<AITool>
        {
            AIFunctionFactory.Create(
                (Func<string, CancellationToken, Task<object>>)SearchKnowledgeAsync,
                AgentMcpContract.SearchKnowledgeToolName,
                "Search security-filtered support knowledge for the selected dataset."),
            AIFunctionFactory.Create(
                (Func<string?, CancellationToken, Task<object>>)GetCustomerHealthAsync,
                AgentMcpContract.GetCustomerHealthToolName,
                "Read customer health from the governed optimized customer health view."),
            AIFunctionFactory.Create(
                (Func<string, string, DateOnly, CancellationToken, Task<object?>>)CreateFollowUpTaskAsync,
                AgentMcpContract.ToolName,
                mcpTool.Description),
        };
        if (tools.Count != AgentMcpContract.ToolNames.Count ||
            !AgentMcpContract.ToolNames.SetEquals(tools.Select(tool => tool.Name)))
        {
            throw new InvalidOperationException("The NICEDA agent must expose exactly the three governed tools.");
        }

        var client = new OpenAIClient(
            new System.ClientModel.ApiKeyCredential(foundry!.ApiKey),
            new OpenAIClientOptions { Endpoint = foundry.Endpoint });
        var agent = client.GetChatClient(foundry.ChatModel).AsIChatClient().AsAIAgent(
            instructions: $"You are NICEDA's customer-success assistant. Current account: {identity.DisplayName}. Dataset mode: {mode}. Use the provided tools to ground answers or create follow-up tasks. Never claim an action succeeded unless the tool confirms it. Cite supporting knowledge document IDs in your answer.",
            tools: tools);

        var response = await agent.RunAsync(request.Message, cancellationToken: cancellationToken);
        var interactionId = Guid.NewGuid().ToString("N");
        var result = new AgentChatResponse(
            response.Text,
            citations.DistinctBy(citation => citation.DocumentId).ToArray(),
            traces.ToArray(),
            interactionId);
        var customers = touchedCustomers
            .Select(pair => new TouchedCustomer(pair.Key, pair.Value.Keys.Order(StringComparer.Ordinal).ToArray()))
            .OrderBy(customer => customer.CustomerId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        _interactions[interactionId] = new ChatInteraction(identity.UserId, mode.ToString(), ChatUseCase, customers);
        await RecordPerCustomerAsync(
            identity.UserId,
            mode.ToString(),
            ChatUseCase,
            interactionId,
            customers,
            result.ToolCalls.LastOrDefault()?.Status,
            request.Message,
            cancellationToken);
        return result;
    }

    public async Task RecordFeedbackAsync(AgentFeedbackRequest request, CancellationToken cancellationToken)
    {
        if (request.Rating is not (-1 or 1))
        {
            throw new ArgumentException("Rating must be 1 (helpful) or -1 (not helpful).", nameof(request));
        }

        if (request.Comment?.Length > 2000)
        {
            throw new ArgumentException("Comment must not exceed 2000 characters.", nameof(request));
        }

        var identity = DemoIdentity.Resolve(request.UserId);
        if (!_interactions.TryGetValue(request.InteractionId, out var interaction))
        {
            throw new KeyNotFoundException("The chat interaction was not found.");
        }

        if (!string.Equals(interaction.UserId, identity.UserId, StringComparison.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("The selected demo identity does not own this interaction.");
        }

        await RecordPerCustomerAsync(
            identity.UserId,
            interaction.Mode,
            RatingUseCase,
            request.InteractionId,
            interaction.Customers,
            request.Rating > 0 ? "helpful" : "not-helpful",
            JsonSerializer.Serialize(new { rating = request.Rating, comment = request.Comment, originalUseCase = interaction.UseCase }),
            cancellationToken);
    }

    public const string ChatUseCase = "agent-chat";
    public const string RatingUseCase = "agent-chat-rating";

    // One record per touched customer so FeedbackService.DeleteCustomerAsync can erase all
    // outcomes and ratings for a customer. Interactions that touched no customer get a single
    // unscoped record because there is no customer data to erase.
    private async Task RecordPerCustomerAsync(
        string userId,
        string mode,
        string useCase,
        string interactionId,
        IReadOnlyList<TouchedCustomer> customers,
        string? outcome,
        string? userInteraction,
        CancellationToken cancellationToken)
    {
        if (customers.Count == 0)
        {
            await feedbackService.RecordAsync(
                new FeedbackSubmission(userId, mode, useCase, $"interaction:{interactionId}",
                    ToolOutcome: outcome, UserInteraction: userInteraction),
                cancellationToken);
            return;
        }

        foreach (var customer in customers)
        {
            await feedbackService.RecordAsync(
                new FeedbackSubmission(
                    userId,
                    mode,
                    useCase,
                    $"interaction:{interactionId}",
                    ToolName: string.Join(",", customer.Sources),
                    ToolOutcome: outcome,
                    UserInteraction: userInteraction,
                    CustomerId: customer.CustomerId),
                cancellationToken);
        }
    }

    // Lets deterministic tests seed an interaction without calling a live model.
    public void TrackInteraction(string interactionId, string userId, string mode, IReadOnlyList<TouchedCustomer> customers) =>
        _interactions[interactionId] = new ChatInteraction(userId, mode, ChatUseCase, customers);

    public sealed record TouchedCustomer(string CustomerId, IReadOnlyList<string> Sources);

    private sealed record ChatInteraction(string UserId, string Mode, string UseCase, IReadOnlyList<TouchedCustomer> Customers);
}
