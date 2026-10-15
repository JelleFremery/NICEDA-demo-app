using System.Text.Json.Nodes;
using Microsoft.Extensions.Http.Resilience;

namespace NICEDA.Web;

public static class DemoApiClientRegistration
{
    public static IHttpClientBuilder AddDemoApiClient(this IServiceCollection services)
    {
        var builder = services.AddHttpClient<DemoApiClient>(client =>
        {
            client.BaseAddress = new("https+http://apiservice");
        });
        builder.AddStandardResilienceHandler(options =>
        {
            options.TotalRequestTimeout.Timeout = TimeSpan.FromMinutes(5);
            options.AttemptTimeout.Timeout = TimeSpan.FromMinutes(5);
            options.CircuitBreaker.SamplingDuration = TimeSpan.FromMinutes(10);
            options.Retry.DisableForUnsafeHttpMethods();
        });
        // The standard handler disables HttpClient.Timeout; restore a bound for
        // response-body buffering too, which happens outside its pipeline.
        builder.ConfigureHttpClient(client => client.Timeout = TimeSpan.FromMinutes(5));
        return builder;
    }
}

public sealed class DemoApiClient(HttpClient httpClient)
{
    public async Task<JsonObject> ChatAsync(string message, string mode, string userId, string requestIdempotencyKey,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/chat")
        {
            Content = JsonContent.Create(new { message, mode, userId, requestIdempotencyKey }),
        };
        request.Headers.CacheControl = new() { NoCache = true, NoStore = true };
        using var response = await httpClient.SendAsync(request, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
        return await response.Content.ReadFromJsonAsync<JsonObject>(cancellationToken)
            ?? throw new InvalidOperationException("The chat API returned an empty response.");
    }

    public async Task FeedbackAsync(string interactionId, string userId, int rating, string comment,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/chat/feedback",
            new { interactionId, userId, rating, comment }, cancellationToken);
        await EnsureSuccessAsync(response, cancellationToken);
    }

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"API error {(int)response.StatusCode} ({response.ReasonPhrase}): {detail}");
        }
    }
}
