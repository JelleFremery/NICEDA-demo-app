using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using NICEDA.ApiService.Crm;
using NICEDA.ApiService.DataInventory;
using NICEDA.ApiService.Legal;
using NICEDA.ApiService.Sql;
using NICEDA.ApiService.Agent;
using NICEDA.ApiService.Audit;
using NICEDA.ApiService.Security;

namespace NICEDA.ApiService.Endpoints;

/// <summary>
/// Minimal "data explorer" endpoints so the Web UI can show each of the
/// four systems' raw ("Before") shape as-is — on purpose, including the
/// awkward joins and inconsistent document shapes. Both shapes respect the
/// selected demo identity; poor structure is not an authorization bypass.
/// </summary>
public static class DataExplorerEndpoints
{
    public static void MapDataExplorerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/data-explorer").WithTags("DataExplorer");

        group.MapGet("/inventory", () => DataInventoryCatalog.Entries);
        group.MapGet("/glossary", () => SemanticGlossary.Terms);

        // NICEDA-PRACTICE-5: resolve demo identity before reading records in
        // either mode. A real deployment must authenticate this context.
        var records = group.MapGroup("").AddEndpointFilter(async (context, next) =>
        {
            try
            {
                context.HttpContext.Items[typeof(DemoUser)] = DemoIdentity.Resolve(
                    context.HttpContext.Request.Headers[AgentMcpContract.UserHeader].ToString());
            }
            catch (UnauthorizedAccessException exception)
            {
                return Results.Problem(
                    title: "The selected demo identity is not authorized",
                    detail: exception.Message,
                    statusCode: StatusCodes.Status403Forbidden);
            }
            return await next(context);
        });

        records.MapGet("/crm/customers", (CrmRepository repository, HttpContext context) =>
            repository.GetAll().Where(customer => Identity(context).CanAccess(customer.Id, customer.Region)));

        records.MapGet("/legal/documents", (LegalRepository repository, HttpContext context) =>
            repository.GetAll().Where(document =>
                Identity(context).CanAccess(LegalKnowledgeProjection.Project(document).CustomerId, null)));

        records.MapGet("/crm/optimized/customers", (CrmRepository repository, HttpContext context) =>
            repository.GetAll().Where(customer => Identity(context).CanAccess(customer.Id, customer.Region))
                .Select(CustomerProfiles.Project));
        records.MapGet("/legal/optimized/documents", (LegalRepository repository, HttpContext context) =>
            repository.GetAll().Select(LegalKnowledgeProjection.Project)
                .Where(document => Identity(context).CanAccess(document.CustomerId, null)));

        records.MapGet("/sql/raw/customers", async (SqlConnection connection, HttpContext context, CancellationToken cancellationToken) =>
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
                if (!Identity(context).CanAccess(reader.GetString(0), reader.GetString(2))) continue;
                var row = new Dictionary<string, object?>();
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
                }
                rows.Add(row);
            }
            return rows;
        });

        records.MapGet("/cosmos/raw-support", async (CosmosClient client, HttpContext context, CancellationToken cancellationToken) =>
        {
            var container = client.GetContainer("niceda", "raw-support");
            var results = new List<JsonObject>();
            using var iterator = container.GetItemQueryIterator<JsonObject>(SupportQuery(Identity(context)));
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
        records.MapGet("/sql/optimized/customer-health", async (SqlConnection connection, HttpContext context, CancellationToken cancellationToken) =>
            (await CustomerHealthQueries.GetCustomerHealthAsync(connection, cancellationToken: cancellationToken))
                .Where(customer => Identity(context).CanAccess(customer.CustomerId, customer.Region)));

        records.MapGet("/sql/optimized/follow-up-tasks", async (SqlConnection connection, HttpContext context, CancellationToken cancellationToken) =>
            (await FollowUpTaskCommands.GetFollowUpTasksAsync(connection, cancellationToken: cancellationToken))
                .Where(task => Identity(context).CanAccess(task.CustomerId, null)));

        // NICEDA-PRACTICE-10: the audit trail of human, system and agent
        // actions, newest first. Mode-independent: it records both modes.
        records.MapGet("/sql/audit-log", async (SqlConnection connection, HttpContext context, CancellationToken cancellationToken) =>
            await AgentActionAuditLog.GetVisibleAsync(connection, Identity(context), cancellationToken));
        records.MapGet("/sql/agent-usage", async (SqlConnection connection, HttpContext context, CancellationToken cancellationToken) =>
            await AgentUsage.GetVisibleAsync(connection, Identity(context), cancellationToken));

        // NICEDA-PRACTICE-4 / NICEDA-PRACTICE-7: metadata envelope +
        // concept-based chunking. Contrast with /cosmos/raw-support above.
        records.MapGet("/cosmos/optimized-support", async (CosmosClient client, HttpContext context, CancellationToken cancellationToken) =>
        {
            var container = client.GetContainer("niceda", "optimized-support");
            var results = new List<JsonObject>();
            using var iterator = container.GetItemQueryIterator<JsonObject>(SupportQuery(Identity(context)));
            while (iterator.HasMoreResults)
            {
                var page = await iterator.ReadNextAsync(cancellationToken);
                results.AddRange(page);
            }
            return results;
        });
    }

    private static DemoUser Identity(HttpContext context) => (DemoUser)context.Items[typeof(DemoUser)]!;

    private static QueryDefinition SupportQuery(DemoUser identity) =>
        identity.IsAdministrator
            ? new QueryDefinition("SELECT * FROM c")
            : new QueryDefinition("""
                SELECT * FROM c
                WHERE ARRAY_CONTAINS(@customers, c.customerId)
                  AND (NOT IS_DEFINED(c.region) OR IS_NULL(c.region) OR c.region = ""
                       OR ARRAY_CONTAINS(@regions, c.region))
                """)
                .WithParameter("@customers", identity.AllowedCustomerIds.ToArray())
                .WithParameter("@regions", identity.AllowedRegions.ToArray());
}
