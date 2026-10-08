namespace NICEDA.ApiService.Agent;

public static class AgentMcpContract
{
    public const string SearchKnowledgeToolName = "search_knowledge";
    public const string GetCustomerHealthToolName = "get_customer_health";
    public const string ToolName = "create_follow_up_task";
    public const string UserHeader = "X-Niceda-User";
    public const string ModeHeader = "X-Niceda-Mode";
    public const string IdempotencyKeyHeader = "X-Niceda-Idempotency-Key";
    public const string CreateFollowUpTaskPath = "/api/data-explorer/sql/optimized/follow-up-tasks";
    public const string ChatPath = "/api/chat";
    public const string ChatFeedbackPath = "/api/chat/feedback";
    public const string McpPath = "/mcp";

    // AppHost passes the MCP server's base URL; the streamable HTTP endpoint lives at /mcp.
    public static bool TryGetMcpEndpoint(string? configuredValue, out Uri endpoint)
    {
        endpoint = null!;
        if (!Uri.TryCreate(configuredValue, UriKind.Absolute, out var parsed))
        {
            return false;
        }

        var path = parsed.AbsolutePath.TrimEnd('/');
        endpoint = path.EndsWith(McpPath, StringComparison.OrdinalIgnoreCase)
            ? parsed
            : new Uri($"{parsed.GetLeftPart(UriPartial.Authority)}{path}{McpPath}");
        return true;
    }

    private static readonly IReadOnlySet<string> BeforeToolNames = new HashSet<string>(StringComparer.Ordinal)
    {
        SearchKnowledgeToolName,
        ToolName,
    };

    private static readonly IReadOnlySet<string> AfterToolNames = new HashSet<string>(StringComparer.Ordinal)
    {
        SearchKnowledgeToolName,
        GetCustomerHealthToolName,
        ToolName,
    };

    public static IReadOnlySet<string> GetToolNames(AgentMode mode) =>
        mode == AgentMode.Before ? BeforeToolNames : AfterToolNames;
}
