using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using NICEDA.ApiService.Crm;
using NICEDA.ApiService.DataInventory;
using NICEDA.ApiService.Legal;

namespace NICEDA.ApiService.Endpoints;

/// <summary>
/// Minimal "data explorer" endpoints so the Web UI can show each of the
/// four systems' raw ("Before") shape as-is — on purpose, including the
/// awkward joins and inconsistent document shapes. No business logic here;
/// the point is to expose the mess, not hide it.
/// </summary>
public static class DataExplorerEndpoints
{
    public static void MapDataExplorerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/data-explorer").WithTags("DataExplorer");

        group.MapGet("/inventory", () => DataInventoryCatalog.Entries);

        group.MapGet("/crm/customers", (CrmRepository repository) => repository.GetAll());

        group.MapGet("/legal/documents", (LegalRepository repository) => repository.GetAll());

        group.MapGet("/sql/raw/customers", async (SqlConnection connection, CancellationToken cancellationToken) =>
        {
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync(cancellationToken);
            }

            const string query = """
                SELECT c.CustomerId, c.CustomerName, c.Region, c.SalesRepEmail,
                       s.StatusCode, k.ContractId, k.StartDate, k.EndDate, k.AnnualValue
                FROM raw.Customer c
                JOIN raw.Contract k ON k.CustomerId = c.CustomerId
                JOIN raw.Status s ON s.StatusId = k.StatusId
                ORDER BY c.CustomerName;
                """;

            await using var command = new SqlCommand(query, connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var rows = new List<Dictionary<string, object?>>();
            while (await reader.ReadAsync(cancellationToken))
            {
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }
            return rows;
        });

        group.MapGet("/cosmos/raw-support", async (CosmosClient client, CancellationToken cancellationToken) =>
        {
            var container = client.GetContainer("niceda", "raw-support");
            var results = new List<JsonObject>();
            using var iterator = container.GetItemQueryIterator<JsonObject>("SELECT * FROM c");
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(page);
            }
            return results;
        });
    }
}
