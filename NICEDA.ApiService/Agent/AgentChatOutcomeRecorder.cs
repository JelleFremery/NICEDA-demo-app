using NICEDA.ApiService.Feedback;

namespace NICEDA.ApiService.Agent;

public sealed class AgentChatOutcomeRecorder(
    FeedbackService feedbackService,
    ILogger<AgentChatOutcomeRecorder> logger)
{
    public async Task<AgentChatResponse> RecordAsync(
        AgentChatResponse response,
        string userId,
        string mode,
        string useCase,
        IReadOnlyList<AgentChatService.TouchedCustomer> customers,
        string? userInteraction,
        CancellationToken cancellationToken)
    {
        try
        {
            if (customers.Count == 0)
            {
                await feedbackService.RecordAsync(
                    new FeedbackSubmission(userId, mode, useCase, $"interaction:{response.InteractionId}",
                        ToolOutcome: response.ToolCalls.LastOrDefault()?.Status, UserInteraction: userInteraction),
                    cancellationToken);
            }
            else
            {
                foreach (var customer in customers)
                {
                    await feedbackService.RecordAsync(
                        new FeedbackSubmission(
                            userId,
                            mode,
                            useCase,
                            $"interaction:{response.InteractionId}",
                            ToolName: string.Join(",", customer.Sources),
                            ToolOutcome: response.ToolCalls.LastOrDefault()?.Status,
                            UserInteraction: userInteraction,
                            CustomerId: customer.CustomerId),
                        cancellationToken);
                }
            }
            return response;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Could not persist feedback for completed interaction {InteractionId}.", response.InteractionId);
            return response with
            {
                FeedbackWarning = "The answer and any confirmed actions completed, but feedback persistence failed. The action outcome is shown above.",
            };
        }
    }
}
