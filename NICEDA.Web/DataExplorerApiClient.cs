using System.Text.Json.Nodes;

namespace NICEDA.Web;

/// <summary>
/// Thin client for the API service's raw and optimized data-explorer endpoints.
/// The API owns projections; the UI renders the returned fields.
/// </summary>
public class DataExplorerApiClient(HttpClient httpClient)
{
    public async Task<List<JsonObject>> GetSystemAsync(string system, string mode,
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
            _ => throw new ArgumentException("Unknown system.", nameof(system))
        };
        using var response = await httpClient.GetAsync($"/api/data-explorer/{path}", cancellationToken);
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

    public Task<List<JsonObject>?> GetCrmCustomersAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<List<JsonObject>>("/api/data-explorer/crm/customers", cancellationToken);

    public Task<List<JsonObject>?> GetLegalDocumentsAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<List<JsonObject>>("/api/data-explorer/legal/documents", cancellationToken);

    public Task<List<JsonObject>?> GetSqlRawCustomersAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<List<JsonObject>>("/api/data-explorer/sql/raw/customers", cancellationToken);

    public Task<List<JsonObject>?> GetCosmosRawSupportAsync(CancellationToken cancellationToken = default) =>
        httpClient.GetFromJsonAsync<List<JsonObject>>("/api/data-explorer/cosmos/raw-support", cancellationToken);
}
