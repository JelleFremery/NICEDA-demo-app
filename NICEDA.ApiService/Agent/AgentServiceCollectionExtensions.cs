using Microsoft.Extensions.DependencyInjection.Extensions;
using NICEDA.ApiService.Audit;
using NICEDA.ApiService.Rag;

namespace NICEDA.ApiService.Agent;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddNicedaAgentServices(this IServiceCollection services)
    {
        // Idempotent (TryAdd): singleton RAG search, embeddings, and feedback store/service.
        services.AddNicedaRagAndFeedback();
        services.AddSingleton<AgentChatOutcomeRecorder>();
        services.TryAddSingleton<AgentActionAuditLog>();
        services.AddSingleton<AgentChatService>();
        return services;
    }
}