using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using NICEDA.ApiService.Crm;
using NICEDA.ApiService.DataInventory;
using NICEDA.ApiService.Legal;
using NICEDA.ApiService.Sql;

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

        group.MapGet("/crm/optimized/customers", (CrmRepository repository) =>
            repository.GetAll().Select(CustomerProfiles.Project));
        group.MapGet("/legal/optimized/documents", (LegalRepository repository) =>
            repository.GetAll().Select(LegalKnowledgeProjection.Project));
        group.MapGet("/glossary", () => SemanticGlossary.Terms);

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

        // NICEDA-PRACTICE-3 / NICEDA-PRACTICE-8 (read side): a single
        // business-named view, not a join the caller has to know how to
        // write. Contrast with /sql/raw/customers above.
        group.MapGet("/sql/optimized/customer-health", async (SqlConnection connection, CancellationToken cancellationToken) =>
            await CustomerHealthQueries.GetCustomerHealthAsync(connection, cancellationToken: cancellationToken));

        group.MapGet("/sql/optimized/follow-up-tasks", async (SqlConnection connection, CancellationToken cancellationToken) =>
            await FollowUpTaskCommands.GetFollowUpTasksAsync(connection, cancellationToken: cancellationToken));

        // NICEDA-PRACTICE-4 / NICEDA-PRACTICE-7: metadata envelope +
        // concept-based chunking. Contrast with /cosmos/raw-support above.
        group.MapGet("/cosmos/optimized-support", async (CosmosClient client, CancellationToken cancellationToken) =>
        {
            var container = client.GetContainer("niceda", "optimized-support");
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
