using System.Text.Json.Serialization;

namespace NICEDA.ApiService.Feedback;

public sealed record FeedbackRecord(
    [property: JsonPropertyName("id")]
    string Id,
    [property: JsonPropertyName("userId")]
    string UserId,
    [property: JsonPropertyName("mode")]
    string Mode,
    [property: JsonPropertyName("useCase")]
    string UseCase,
    [property: JsonPropertyName("entity")]
    string Entity,
    [property: JsonPropertyName("toolName")]
    string? ToolName,
    [property: JsonPropertyName("toolOutcome")]
    string? ToolOutcome,
    [property: JsonPropertyName("userInteraction")]
    string? UserInteraction,
    [property: JsonPropertyName("failure")]
    string? Failure,
    [property: JsonPropertyName("occurredAtUtc")]
    DateTime OccurredAtUtc,
    [property: JsonPropertyName("customerId")]
    string? CustomerId = null);

public sealed record FeedbackSubmission(
    string UserId,
    string Mode,
    string UseCase,
    string Entity,
    string? ToolName = null,
    string? ToolOutcome = null,
    string? UserInteraction = null,
    string? Failure = null,
    string? CustomerId = null);
