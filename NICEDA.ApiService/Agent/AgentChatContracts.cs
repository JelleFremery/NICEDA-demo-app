namespace NICEDA.ApiService.Agent;

public sealed record AgentChatRequest(string Mode, string UserId, string Message);

public sealed record AgentToolTrace(string Tool, string Status, string? Detail);

public sealed record AgentChatResponse(
    string Answer,
    IReadOnlyList<KnowledgeCitation> Citations,
    IReadOnlyList<AgentToolTrace> ToolCalls,
    string InteractionId);

public sealed record KnowledgeCitation(string DocumentId, string CustomerId, string? Concept, string Excerpt);

public sealed record AgentFeedbackRequest(string InteractionId, string UserId, int Rating, string? Comment);

public sealed record CreateFollowUpTaskRequest(string CustomerId, string Reason, DateOnly DueDate);
