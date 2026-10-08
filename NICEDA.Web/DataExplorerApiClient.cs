using System.Text.Json.Nodes;

namespace NICEDA.Web;

/// <summary>
/// Thin client for the API service's data-explorer endpoints. No business
/// logic — the UI just renders whatever shape each system returns,
/// inconsistencies and all, since showing that mess *is* the "Before" demo.
/// </summary>
public class DataExplorerApiClient(HttpClient httpClient)
{
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
