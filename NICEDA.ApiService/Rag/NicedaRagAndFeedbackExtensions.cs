using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Http.Resilience;
using NICEDA.ApiService.Feedback;

namespace NICEDA.ApiService.Rag;

public static class NicedaRagAndFeedbackExtensions
{
    public static IServiceCollection AddNicedaRagAndFeedback(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        if (!services.Any(descriptor => descriptor.ServiceType == typeof(IEmbeddingService)))
        {
            services.AddHttpClient(nameof(FoundryEmbeddingService))
                .AddStandardResilienceHandler(ConfigureEmbeddingResilience);
        }
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

    // Embedding POSTs are idempotent, so retries stay on; each attempt gets a
    // budget that tolerates a slow Foundry response or a batched index build.
    internal static void ConfigureEmbeddingResilience(HttpStandardResilienceOptions options)
    {
        options.AttemptTimeout.Timeout = TimeSpan.FromSeconds(90);
        options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(4);
        options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(5);
    }
}