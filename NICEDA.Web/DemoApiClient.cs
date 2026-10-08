using System.Text.Json.Nodes;

namespace NICEDA.Web;

public sealed class DemoApiClient(HttpClient httpClient)
{
    public async Task<JsonObject> ChatAsync(string message, string mode, string userId,
        CancellationToken cancellationToken = default)
    {
        using var response = await httpClient.PostAsJsonAsync("/api/chat",
            new { message, mode, userId }, cancellationToken);
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
