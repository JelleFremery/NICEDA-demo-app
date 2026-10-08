namespace NICEDA.ApiService.Feedback;

public interface IFeedbackStore
{
    Task SaveAsync(FeedbackRecord record, CancellationToken cancellationToken = default);

    Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default);
}

public sealed class FeedbackService(IFeedbackStore store)
{
    public async Task<FeedbackRecord> RecordAsync(
        FeedbackSubmission submission, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.UserId);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.Mode);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.UseCase);
        ArgumentException.ThrowIfNullOrWhiteSpace(submission.Entity);

        var record = new FeedbackRecord(
            Guid.NewGuid().ToString("N"),
            submission.UserId,
            submission.Mode,
            submission.UseCase,
            submission.Entity,
            submission.ToolName,
            submission.ToolOutcome,
            submission.UserInteraction,
            submission.Failure,
            DateTime.UtcNow,
            submission.CustomerId);

        await store.SaveAsync(record, cancellationToken);
        return record;
    }

    public Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        return store.DeleteCustomerAsync(customerId, cancellationToken);
    }
}
