using NICEDA.ApiService.Agent;
using Microsoft.AspNetCore.Http;

namespace NICEDA.Tests;

public sealed class McpToolContractTests
{
    [Fact]
    public void AgentRegistersOnlyTheGovernedMcpActionAndPassesContextOutOfBand()
    {
        Assert.Equal(
            new[] { "create_follow_up_task", "get_customer_health", "search_knowledge" },
            AgentMcpContract.GetToolNames(AgentMode.After).Order(StringComparer.Ordinal));
        Assert.Equal(
            new[] { "create_follow_up_task", "search_knowledge" },
            AgentMcpContract.GetToolNames(AgentMode.Before).Order(StringComparer.Ordinal));
        Assert.Equal("create_follow_up_task", AgentMcpContract.ToolName);
        Assert.Equal("/api/data-explorer/sql/optimized/follow-up-tasks", AgentMcpContract.CreateFollowUpTaskPath);
        Assert.Equal("X-Niceda-User", AgentMcpContract.UserHeader);
        Assert.Equal("X-Niceda-Mode", AgentMcpContract.ModeHeader);
        Assert.Equal("X-Niceda-Idempotency-Key", AgentMcpContract.IdempotencyKeyHeader);
        Assert.Equal("/api/chat", AgentMcpContract.ChatPath);
        Assert.Equal("/api/chat/feedback", AgentMcpContract.ChatFeedbackPath);
    }

    [Fact]
    public async Task GovernedFollowUpEndpoint_RejectsMissingTrustedContext()
    {
        var request = new DefaultHttpContext();
        var result = await AgentChatEndpoints.HandleCreateFollowUpTaskAsync(
            new CreateFollowUpTaskRequest("techorama", "Resolve export issue", DateOnly.FromDateTime(DateTime.UtcNow)),
            request,
            connection: null!,
            CancellationToken.None);

        Assert.Equal(400, result.GetType().GetProperty("StatusCode")?.GetValue(result));
    }

    [Fact]
    public async Task GovernedFollowUpEndpoint_RejectsMissingIdempotencyContext()
    {
        var request = new DefaultHttpContext();
        request.Request.Headers[AgentMcpContract.UserHeader] = "jane";
        request.Request.Headers[AgentMcpContract.ModeHeader] = "After";
        var result = await AgentChatEndpoints.HandleCreateFollowUpTaskAsync(
            new CreateFollowUpTaskRequest("techorama", "Resolve export issue", DateOnly.FromDateTime(DateTime.UtcNow)),
            request,
            connection: null!,
            CancellationToken.None);

        Assert.Equal(400, result.GetType().GetProperty("StatusCode")?.GetValue(result));
    }

    [Fact]
    public async Task GovernedFollowUpEndpoint_RejectsBeforeModeAndOutOfScopeCustomers()
    {
        var request = new DefaultHttpContext();
        request.Request.Headers[AgentMcpContract.UserHeader] = "jane";
        request.Request.Headers[AgentMcpContract.ModeHeader] = "Before";
        var beforeResult = await AgentChatEndpoints.HandleCreateFollowUpTaskAsync(
            new CreateFollowUpTaskRequest("techorama", "Resolve export issue", DateOnly.FromDateTime(DateTime.UtcNow)),
            request,
            connection: null!,
            CancellationToken.None);
        Assert.Equal(400, beforeResult.GetType().GetProperty("StatusCode")?.GetValue(beforeResult));

        request.Request.Headers[AgentMcpContract.ModeHeader] = "After";
        var unauthorizedResult = await AgentChatEndpoints.HandleCreateFollowUpTaskAsync(
            new CreateFollowUpTaskRequest("contoso", "Resolve export issue", DateOnly.FromDateTime(DateTime.UtcNow)),
            request,
            connection: null!,
            CancellationToken.None);
        Assert.Equal(403, unauthorizedResult.GetType().GetProperty("StatusCode")?.GetValue(unauthorizedResult));
    }
}
