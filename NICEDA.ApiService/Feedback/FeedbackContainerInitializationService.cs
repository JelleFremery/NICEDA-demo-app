using Microsoft.Extensions.Hosting;

namespace NICEDA.ApiService.Feedback;

public sealed class FeedbackContainerInitializationService(CosmosFeedbackStore feedbackStore) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken) =>
        feedbackStore.EnsureCreatedAsync(cancellationToken);

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
