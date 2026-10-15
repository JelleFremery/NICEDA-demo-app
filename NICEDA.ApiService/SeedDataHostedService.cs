using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using NICEDA.ApiService.Audit;
using NICEDA.ApiService.DataLifecycle;
using NICEDA.ApiService.Sql;
using NICEDA.ApiService.SupportDocs;

namespace NICEDA.ApiService;

/// <summary>
/// Runs once at startup to ensure the "Before" SQL schema and Cosmos
/// container are created and seeded. CRM/SharePoint seed data needs no
/// hosted service — it's loaded synchronously by their repositories.
/// </summary>
public sealed class SeedDataHostedService(
    IServiceScopeFactory scopeFactory,
    RawSqlSeeder sqlSeeder,
    OptimizedSqlSeeder optimizedSqlSeeder,
    RawSupportSeeder supportSeeder,
    OptimizedSupportSeeder optimizedSupportSeeder,
    NICEDA.ApiService.Crm.CrmRepository crm,
    NICEDA.ApiService.Legal.LegalRepository legal,
    NICEDA.ApiService.Rag.KnowledgeSearchService knowledge,
    ILogger<SeedDataHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var startupDeadline = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startupDeadline.CancelAfter(TimeSpan.FromMinutes(2));
        cancellationToken = startupDeadline.Token;
        using var scope = scopeFactory.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<SqlConnection>();
        var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();

        // Compose starts containers, not Aspire's database provisioning loop.
        // The demo therefore creates its own empty database once SQL is ready.
        // A real deployment would provision databases outside application boot.
        await EnsureDatabaseReadyAsync(connection.ConnectionString, cancellationToken);
        await EnsureCosmosReadyAsync(cosmosClient, cancellationToken);
        logger.LogInformation("Seeding raw SQL schema...");
        await sqlSeeder.EnsureSeededAsync(connection, cancellationToken);

        logger.LogInformation("Creating optimized SQL schema (view + command table)...");
        await optimizedSqlSeeder.EnsureCreatedAsync(connection, cancellationToken);

        logger.LogInformation("Creating and seeding audit log...");
        var liveCustomerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new SqlCommand(
            "SELECT CustomerId FROM raw.Customer WHERE CustomerId NOT IN (SELECT CustomerId FROM raw.DeletedCustomer);",
            connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) liveCustomerIds.Add(reader.GetString(0));
        }
        await AgentActionAuditLog.EnsureCreatedAndSeededAsync(connection, liveCustomerIds, cancellationToken);

        // Durable markers survive a process restart. Finish every cross-store
        // deletion before seeding support data or declaring startup ready.
        logger.LogInformation("Reconciling marked customer deletions...");
        await scope.ServiceProvider.GetRequiredService<CustomerDataLifecycle>()
            .ReconcilePendingDeletionsAsync(cancellationToken);

        var customerIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using (var command = new SqlCommand("SELECT CustomerId FROM raw.Customer;", connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken)) customerIds.Add(reader.GetString(0));
        }
        // NICEDA-PRACTICE-6: persisted deletion markers prevent the static demo
        // seeds and mocked projections from resurrecting deleted customers.
        foreach (var customer in crm.GetAll().Where(c => !customerIds.Contains(c.Id)))
        {
            crm.RemoveCustomer(customer.Id);
            legal.RemoveCustomer(customer.Id);
            knowledge.RemoveCustomer(customer.Id);
        }

        logger.LogInformation("Seeding raw-support Cosmos container...");
        await supportSeeder.EnsureSeededAsync(cosmosClient, customerIds, cancellationToken);

        logger.LogInformation("Seeding optimized-support Cosmos container...");
        await optimizedSupportSeeder.EnsureSeededAsync(cosmosClient, customerIds, cancellationToken);

        logger.LogInformation("Seeding complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private async Task EnsureDatabaseReadyAsync(string connectionString, CancellationToken ct)
    {
        var settings = new SqlConnectionStringBuilder(connectionString)
        {
            InitialCatalog = "master",
            ConnectTimeout = 5,
        };
        while (true)
        {
            try
            {
                await using var master = new SqlConnection(settings.ConnectionString);
                await master.OpenAsync(ct);
                await using var create = new SqlCommand(
                    "IF DB_ID('niceda') IS NULL CREATE DATABASE niceda;", master);
                await create.ExecuteNonQueryAsync(ct);
                return;
            }
            catch (SqlException ex) when (!ct.IsCancellationRequested
                && ex.Number is -2 or 0 or 2 or 53 or 64 or 233 or 10060 or 10061)
            {
                logger.LogWarning("Waiting for SQL container readiness (SQL error {ErrorNumber})", ex.Number);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }

    private async Task EnsureCosmosReadyAsync(CosmosClient client, CancellationToken ct)
    {
        while (true)
        {
            try
            {
                await client.ReadAccountAsync().WaitAsync(ct);
                return;
            }
            catch (HttpRequestException ex) when (!ct.IsCancellationRequested
                && ex.HttpRequestError == HttpRequestError.ConnectionError)
            {
                logger.LogWarning("Waiting for Cosmos emulator network readiness");
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
            catch (CosmosException ex) when (!ct.IsCancellationRequested
                && ex.StatusCode is System.Net.HttpStatusCode.ServiceUnavailable
                    or System.Net.HttpStatusCode.RequestTimeout)
            {
                logger.LogWarning("Waiting for Cosmos emulator readiness ({StatusCode})", ex.StatusCode);
                await Task.Delay(TimeSpan.FromSeconds(2), ct);
            }
        }
    }
}
