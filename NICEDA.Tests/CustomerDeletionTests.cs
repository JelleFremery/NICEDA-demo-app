using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;
using NICEDA.ApiService.DataLifecycle;

namespace NICEDA.Tests;

/// <summary>
/// NICEDA-PRACTICE-6: proves <see cref="CustomerDeletionCommand"/> actually
/// removes data from every store it touches. Uses a dedicated scratch
/// customer id (never part of the CRM/demo-narrative seed) inserted
/// directly via raw SQL/Cosmos connections, so this test never risks
/// deleting the Betabit/Fenetre/Techorama/Contoso/Fabrikam demo data.
/// </summary>
[Collection(nameof(AppHostCollection))]
public class CustomerDeletionTests(AppHostFixture fixture)
{
    private const string ScratchCustomerId = "scratch-deletion-test";

    [DockerFact]
    public async Task DeleteCustomerData_RemovesRowsAndDocumentsAcrossAllStores()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        var sqlConnectionString = await fixture.GetSqlConnectionStringAsync();
        var cosmosConnectionString = await fixture.GetCosmosConnectionStringAsync();
        Assert.NotNull(sqlConnectionString);
        Assert.NotNull(cosmosConnectionString);

        await using var sqlConnection = new SqlConnection(sqlConnectionString);
        await sqlConnection.OpenAsync();
        using var cosmosClient = new CosmosClient(cosmosConnectionString, new CosmosClientOptions
        {
            UseSystemTextJsonSerializerWithOptions = new System.Text.Json.JsonSerializerOptions(),
            ConnectionMode = ConnectionMode.Gateway,
            RequestTimeout = TimeSpan.FromSeconds(15),
            MaxRetryAttemptsOnRateLimitedRequests = 0,
        });

        await SeedScratchCustomerAsync(sqlConnection, cosmosClient);
        await NICEDA.ApiService.Sql.FollowUpTaskCommands.CreateFollowUpTaskAsync(sqlConnection,
            new(ScratchCustomerId, "Scratch deletion check",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)), "tests"));

        await CustomerDeletionCommand.DeleteCustomerDataAsync(sqlConnection, cosmosClient, ScratchCustomerId, deadline.Token);

        await using (var command = new SqlCommand(
            """
            SELECT (SELECT COUNT(*) FROM raw.Customer WHERE CustomerId=@id)
                 + (SELECT COUNT(*) FROM raw.Contract WHERE CustomerId=@id)
                 + (SELECT COUNT(*) FROM optimized.FollowUpTask WHERE CustomerId=@id);
            """, sqlConnection))
        {
            command.Parameters.AddWithValue("@id", ScratchCustomerId);
            var remaining = (int)(await command.ExecuteScalarAsync())!;
            Assert.Equal(0, remaining);
        }
        await using (var marker = new SqlCommand(
            "SELECT COUNT(*) FROM raw.DeletedCustomer WHERE CustomerId=@id;", sqlConnection))
        {
            marker.Parameters.AddWithValue("@id", ScratchCustomerId);
            Assert.Equal(1, (int)(await marker.ExecuteScalarAsync())!);
        }

        foreach (var containerId in new[] { "raw-support", "optimized-support" })
        {
            var container = cosmosClient.GetContainer("niceda", containerId);
            using var iterator = container.GetItemQueryIterator<int>(
                new QueryDefinition("SELECT VALUE COUNT(1) FROM c"),
                requestOptions: new QueryRequestOptions { PartitionKey = new PartitionKey(ScratchCustomerId) });
            var page = await iterator.ReadNextAsync(deadline.Token);
            Assert.Equal(0, page.FirstOrDefault());
        }
    }

    private static async Task SeedScratchCustomerAsync(SqlConnection sqlConnection, CosmosClient cosmosClient)
    {
        await CustomerDeletionCommand.DeleteCustomerDataAsync(sqlConnection, cosmosClient, ScratchCustomerId);

        await using (var customerCommand = new SqlCommand(
            "INSERT INTO raw.Customer (CustomerId, CustomerName, Region, SalesRepEmail) VALUES (@id, @name, @region, @rep);",
            sqlConnection))
        {
            customerCommand.Parameters.AddWithValue("@id", ScratchCustomerId);
            customerCommand.Parameters.AddWithValue("@name", "Scratch Deletion Test Co");
            customerCommand.Parameters.AddWithValue("@region", "EU");
            customerCommand.Parameters.AddWithValue("@rep", "test@niceda.example");
            await customerCommand.ExecuteNonQueryAsync();
        }

        int contractId;
        await using (var contractCommand = new SqlCommand(
            """
            INSERT INTO raw.Contract (CustomerId, StatusId, StartDate, EndDate, AnnualValue)
            OUTPUT INSERTED.ContractId
            VALUES (@customerId, 1, @startDate, @endDate, 1000);
            """,
            sqlConnection))
        {
            contractCommand.Parameters.AddWithValue("@customerId", ScratchCustomerId);
            contractCommand.Parameters.AddWithValue("@startDate", DateTime.UtcNow.Date);
            contractCommand.Parameters.AddWithValue("@endDate", DateTime.UtcNow.AddYears(1).Date);
            contractId = (int)(await contractCommand.ExecuteScalarAsync())!;
        }

        await using (var lineCommand = new SqlCommand(
            """
            INSERT INTO raw.ContractLine (ContractId, ProductCode, Description, Quantity, UnitPrice)
            VALUES (@contractId, 'TEST', 'Scratch test line', 1, 1000);
            """,
            sqlConnection))
        {
            lineCommand.Parameters.AddWithValue("@contractId", contractId);
            await lineCommand.ExecuteNonQueryAsync();
        }

        foreach (var containerId in new[] { "raw-support", "optimized-support" })
        {
            var container = cosmosClient.GetContainer("niceda", containerId);
            var document = new System.Text.Json.Nodes.JsonObject
            {
                ["id"] = $"{containerId}-scratch-doc",
                ["customerId"] = ScratchCustomerId,
                ["note"] = "Scratch document for deletion test.",
            };
            await container.UpsertItemAsync(document, new PartitionKey(ScratchCustomerId));
        }
    }
}
