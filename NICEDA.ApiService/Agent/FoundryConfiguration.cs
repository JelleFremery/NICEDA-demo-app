namespace NICEDA.ApiService.Agent;

public sealed record FoundryConfiguration(Uri Endpoint, string ApiKey, string ChatModel, string EmbeddingModel)
{
    public static bool TryLoad(IConfiguration configuration, out FoundryConfiguration? settings)
    {
        settings = null;
        var endpointValue = configuration["Foundry:Endpoint"];
        var apiKey = configuration["Foundry:ApiKey"];
        if (!Uri.TryCreate(endpointValue, UriKind.Absolute, out var endpoint) ||
            string.IsNullOrWhiteSpace(apiKey))
        {
            return false;
        }

        var endpointPath = endpoint.AbsolutePath.TrimEnd('/');
        if (!endpointPath.EndsWith("/openai/v1", StringComparison.OrdinalIgnoreCase) &&
            !endpointPath.EndsWith("/v1", StringComparison.OrdinalIgnoreCase))
        {
            endpoint = new Uri($"{endpoint.ToString().TrimEnd('/')}/openai/v1/");
        }
        else if (!endpoint.AbsoluteUri.EndsWith("/", StringComparison.Ordinal))
        {
            endpoint = new Uri($"{endpoint}/");
        }

        settings = new FoundryConfiguration(
            endpoint,
            apiKey,
            configuration["Foundry:ChatModel"] ?? "gpt-6.1-sol",
            configuration["Foundry:EmbeddingModel"] ?? "text-embedding-3-small");
        return true;
    }
}
