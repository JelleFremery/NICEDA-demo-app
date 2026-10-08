using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using NICEDA.ApiService.Feedback;

namespace NICEDA.ApiService.Rag;

public static class NicedaRagAndFeedbackExtensions
{
    public static IServiceCollection AddNicedaRagAndFeedback(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddHttpClient(nameof(FoundryEmbeddingService));
        services.TryAddSingleton<IEmbeddingService>(serviceProvider =>
            new FoundryEmbeddingService(
                serviceProvider.GetRequiredService<IHttpClientFactory>()
                    .CreateClient(nameof(FoundryEmbeddingService)),
                serviceProvider.GetRequiredService<IConfiguration>()));
        services.TryAddSingleton<IKnowledgeDocumentSource, CosmosKnowledgeDocumentSource>();
        services.TryAddSingleton<KnowledgeSearchService>();
        services.TryAddSingleton<CosmosFeedbackStore>();
        services.TryAddSingleton<IFeedbackStore>(serviceProvider =>
            serviceProvider.GetRequiredService<CosmosFeedbackStore>());
        services.TryAddSingleton<FeedbackService>();
        services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IHostedService, FeedbackContainerInitializationService>());
        return services;
    }
}