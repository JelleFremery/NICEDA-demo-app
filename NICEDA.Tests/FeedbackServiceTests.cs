using NICEDA.ApiService.Feedback;

namespace NICEDA.Tests;

public sealed class FeedbackServiceTests
{
    [Fact]
    public async Task RecordAsync_PersistsUseCaseEntityOutcomeInteractionAndFailure()
    {
        var store = new FakeFeedbackStore();
        var service = new FeedbackService(store);
        var submission = new FeedbackSubmission(
            "jane",
            "after",
            "identify-customer-risk",
            "customer:betabit",
            "search_knowledge",
            "failed",
            "thumbs-down",
            "The answer missed the renewal escalation.",
            "betabit");

        var record = await service.RecordAsync(submission);

        Assert.Same(record, store.SavedRecord);
        Assert.False(string.IsNullOrWhiteSpace(record.Id));
        Assert.Equal("identify-customer-risk", record.UseCase);
        Assert.Equal("customer:betabit", record.Entity);
        Assert.Equal("search_knowledge", record.ToolName);
        Assert.Equal("failed", record.ToolOutcome);
        Assert.Equal("thumbs-down", record.UserInteraction);
        Assert.Equal("The answer missed the renewal escalation.", record.Failure);
        Assert.Equal("betabit", record.CustomerId);
        Assert.Equal(DateTimeKind.Utc, record.OccurredAtUtc.Kind);
    }

    [Fact]
    public async Task DeleteCustomerAsync_DelegatesToFeedbackStore()
    {
        var store = new FakeFeedbackStore();
        var service = new FeedbackService(store);

        await service.DeleteCustomerAsync("betabit");

        Assert.Equal("betabit", store.DeletedCustomerId);
    }

    [Fact]
    public async Task RecordAsync_AllowsFeedbackWithoutKnownCustomer()
    {
        var store = new FakeFeedbackStore();
        var service = new FeedbackService(store);

        var record = await service.RecordAsync(
            new FeedbackSubmission("jane", "after", "retrieval-failure", "interaction:123"));

        Assert.Null(record.CustomerId);
        Assert.Same(record, store.SavedRecord);
    }

    [Fact]
    public async Task RecordAsync_PropagatesPersistenceFailure()
    {
        var service = new FeedbackService(new FailingFeedbackStore());

        await Assert.ThrowsAsync<InvalidOperationException>(() => service.RecordAsync(
            new FeedbackSubmission("jane", "after", "use-case", "entity")));
    }

    private sealed class FakeFeedbackStore : IFeedbackStore
    {
        public FeedbackRecord? SavedRecord { get; private set; }
        public string? DeletedCustomerId { get; private set; }

        public Task SaveAsync(FeedbackRecord record, CancellationToken cancellationToken = default)
        {
            SavedRecord = record;
            return Task.CompletedTask;
        }

        public Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default)
        {
            DeletedCustomerId = customerId;
            return Task.CompletedTask;
        }
    }

    private sealed class FailingFeedbackStore : IFeedbackStore
    {
        public Task SaveAsync(FeedbackRecord record, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Persistence failed.");

        public Task DeleteCustomerAsync(string customerId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("Persistence failed.");
    }
}
