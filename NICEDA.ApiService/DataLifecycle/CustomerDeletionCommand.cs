using Microsoft.Azure.Cosmos;
using Microsoft.Data.SqlClient;

namespace NICEDA.ApiService.DataLifecycle;

/// <summary>
/// NICEDA-PRACTICE-6 (design for deletion): a single command that fans a
/// "delete this customer's data" request out across every store that can
/// hold it — SQL (raw + optimized) and both Cosmos containers (raw +
/// optimized support). The deck's point is that "please delete this
/// customer" is easy to say and hard to guarantee once data has been
/// copied/projected into multiple shapes; this command is the thing that
/// makes the guarantee instead of leaving deletion as an afterthought.
///
/// Cosmos containers here are partitioned by <c>/customerId</c>
/// specifically so a customer's documents are a single partition — no
/// cross-partition fan-out query is needed to find everything to delete.
/// </summary>
public static class CustomerDeletionCommand
{
    public static async Task DeleteCustomerDataAsync(
        SqlConnection sqlConnection,
        CosmosClient cosmosClient,
        string customerId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }
        if (customerId.Length > 50)
            throw new ArgumentException("CustomerId cannot exceed 50 characters.", nameof(customerId));
        customerId = customerId.Trim().ToLowerInvariant();

        await DeleteSqlDataAsync(sqlConnection, customerId, cancellationToken);
        await DeleteCosmosContainerDataAsync(cosmosClient, "raw-support", customerId, cancellationToken);
        await DeleteCosmosContainerDataAsync(cosmosClient, "optimized-support", customerId, cancellationToken);
    }

    private static async Task DeleteSqlDataAsync(SqlConnection connection, string customerId, CancellationToken cancellationToken)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        // Child rows first to satisfy foreign keys: ContractLine -> Contract -> Customer.
        const string sql = """
            IF NOT EXISTS (SELECT 1 FROM raw.DeletedCustomer WHERE CustomerId=@customerId)
                INSERT INTO raw.DeletedCustomer (CustomerId, DeletedAtUtc)
                VALUES (@customerId, SYSUTCDATETIME());

            DELETE cl FROM raw.ContractLine cl
            JOIN raw.Contract k ON k.ContractId = cl.ContractId
            WHERE k.CustomerId = @customerId;

            DELETE FROM raw.Contract WHERE CustomerId = @customerId;
            DELETE FROM raw.Customer WHERE CustomerId = @customerId;
            DELETE FROM optimized.FollowUpTask WHERE CustomerId = @customerId;
            """;

        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task DeleteCosmosContainerDataAsync(
        CosmosClient client, string containerId, string customerId, CancellationToken cancellationToken)
    {
        var container = client.GetContainer("niceda", containerId);
        var partitionKey = new PartitionKey(customerId);

        using var iterator = container.GetItemQueryIterator<System.Text.Json.Nodes.JsonObject>(
            new QueryDefinition("SELECT c.id FROM c"),
            requestOptions: new QueryRequestOptions { PartitionKey = partitionKey });

        var ids = new List<string>();
        while (iterator.HasMoreResults)
        {
            var page = await iterator.ReadNextAsync(cancellationToken);
            ids.AddRange(page.Select(d => d["id"]!.GetValue<string>()));
        }

        foreach (var id in ids)
        {
            await container.DeleteItemAsync<System.Text.Json.Nodes.JsonObject>(id, partitionKey, cancellationToken: cancellationToken);
        }
    }
}
