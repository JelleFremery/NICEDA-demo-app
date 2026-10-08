using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NICEDA.ApiService.Feedback;
using NICEDA.ApiService.Rag;

namespace NICEDA.Tests;

public sealed class RagServiceRegistrationTests
{
    [Fact]
    public void AddNicedaRagAndFeedback_IsIdempotentAndUsesSingletons()
    {
        var services = new ServiceCollection();

        services.AddNicedaRagAndFeedback();
        services.AddNicedaRagAndFeedback();

        Assert.Single(services, d => d.ServiceType == typeof(KnowledgeSearchService) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Single(services, d => d.ServiceType == typeof(IEmbeddingService) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Single(services, d => d.ServiceType == typeof(IKnowledgeDocumentSource) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Single(services, d => d.ServiceType == typeof(FeedbackService) && d.Lifetime == ServiceLifetime.Singleton);
        Assert.Single(services, d => d.ServiceType == typeof(IHostedService)
            && d.ImplementationType == typeof(FeedbackContainerInitializationService));
    }
}