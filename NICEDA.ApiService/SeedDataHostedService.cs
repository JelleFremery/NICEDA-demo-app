using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
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
    RawSupportSeeder supportSeeder,
    ILogger<SeedDataHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var connection = scope.ServiceProvider.GetRequiredService<SqlConnection>();
        var cosmosClient = scope.ServiceProvider.GetRequiredService<CosmosClient>();

        logger.LogInformation("Seeding raw SQL schema...");
        await sqlSeeder.EnsureSeededAsync(connection, cancellationToken);

        logger.LogInformation("Seeding raw-support Cosmos container...");
        await supportSeeder.EnsureSeededAsync(cosmosClient, cancellationToken);

        logger.LogInformation("Seeding complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
