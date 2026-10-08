using NICEDA.ApiService.Rag;

namespace NICEDA.ApiService.Agent;

public static class AgentServiceCollectionExtensions
{
    public static IServiceCollection AddNicedaAgentServices(this IServiceCollection services)
    {
        // Idempotent (TryAdd): singleton RAG search, embeddings, and feedback store/service.
        services.AddNicedaRagAndFeedback();
        services.AddSingleton<AgentChatOutcomeRecorder>();
        services.AddSingleton<AgentChatService>();
        return services;
    }
}