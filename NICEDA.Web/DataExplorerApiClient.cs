using System.Text.Json.Nodes;

namespace NICEDA.Web;

/// <summary>
/// Thin client for the API service's raw and optimized data-explorer endpoints.
/// The API owns projections; the UI renders the returned fields.
/// </summary>
public class DataExplorerApiClient(HttpClient httpClient)
{
    public async Task<List<FollowUpTask>> GetFollowUpTasksAsync(string userId,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/data-explorer/sql/optimized/follow-up-tasks");
        request.Headers.Add("X-Niceda-User", userId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"API error {(int)response.StatusCode} ({response.ReasonPhrase}): {detail}");
        }
        return await response.Content.ReadFromJsonAsync<List<FollowUpTask>>(cancellationToken)
            ?? throw new InvalidOperationException("The task API returned an empty response.");
    }

    public async Task<List<JsonObject>> GetSystemAsync(string system, string mode, string userId,
        CancellationToken cancellationToken = default)
    {
        var path = (system, mode) switch
        {
            ("CRM", "after") => "crm/optimized/customers",
            ("CRM", _) => "crm/customers",
            ("SQL", "after") => "sql/optimized/customer-health",
            ("SQL", _) => "sql/raw/customers",
            ("Cosmos DB", "after") => "cosmos/optimized-support",
            ("Cosmos DB", _) => "cosmos/raw-support",
            ("SharePoint", "after") => "legal/optimized/documents",
            ("SharePoint", _) => "legal/documents",
            ("Audit log", _) => "sql/audit-log",
            ("Agent usage", _) => "sql/agent-usage",
            _ => throw new ArgumentException("Unknown system.", nameof(system))
        };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/data-explorer/{path}");
        request.Headers.Add("X-Niceda-User", userId);
        using var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException(
                $"API error {(int)response.StatusCode} ({response.ReasonPhrase}): {detail}");
        }

        return await response.Content.ReadFromJsonAsync<List<JsonObject>>(cancellationToken)
            ?? throw new InvalidOperationException("The explorer API returned an empty response.");
    }

    public Task<List<JsonObject>?> GetDataInventoryAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<List<JsonObject>>("/api/data-explorer/inventory", cancellationToken);

}

public sealed record FollowUpTask(
    int FollowUpTaskId, string CustomerId, string Reason, DateTime DueDate,
    string CreatedBy, DateTime CreatedAtUtc, bool IsCompleted);
