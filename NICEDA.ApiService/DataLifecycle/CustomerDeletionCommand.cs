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
    public static async Task<IReadOnlyList<string>> GetDeletedCustomerIdsAsync(
        SqlConnection sqlConnection, CancellationToken cancellationToken = default)
    {
        if (sqlConnection.State != System.Data.ConnectionState.Open)
        {
            await sqlConnection.OpenAsync(cancellationToken);
        }

        var customerIds = new List<string>();
        await using var command = new SqlCommand(
            "SELECT CustomerId FROM raw.DeletedCustomer ORDER BY CustomerId;", sqlConnection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            customerIds.Add(reader.GetString(0));
        }

        return customerIds;
    }

    public static async Task EnsureCustomerIsNotDeletedAsync(
        SqlConnection sqlConnection, string customerId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        if (customerId.Trim().Length > 50)
            throw new ArgumentException("CustomerId cannot exceed 50 characters.", nameof(customerId));
        if (sqlConnection.State != System.Data.ConnectionState.Open)
        {
            await sqlConnection.OpenAsync(cancellationToken);
        }

        await using var command = new SqlCommand(
            "SELECT COUNT(*) FROM raw.DeletedCustomer WHERE CustomerId=@customerId;", sqlConnection);
        command.Parameters.AddWithValue("@customerId", customerId.Trim().ToLowerInvariant());
        if ((int)(await command.ExecuteScalarAsync(cancellationToken))! > 0)
        {
            throw new InvalidOperationException(
                $"Customer '{customerId}' is marked for deletion; customer-linked writes are not allowed.");
        }
    }

    public static async Task ExecuteCustomerWriteIfNotDeletedAsync(
        SqlConnection sqlConnection,
        string customerId,
        Func<CancellationToken, Task> writeAsync,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        ArgumentNullException.ThrowIfNull(writeAsync);
        customerId = customerId.Trim().ToLowerInvariant();
        if (customerId.Length > 50)
            throw new ArgumentException("CustomerId cannot exceed 50 characters.", nameof(customerId));

        if (sqlConnection.State != System.Data.ConnectionState.Open)
        {
            await sqlConnection.OpenAsync(cancellationToken);
        }

        await using var transaction =
            (SqlTransaction)await sqlConnection.BeginTransactionAsync(cancellationToken);
        await AcquireCustomerLifecycleLockAsync(
            sqlConnection, transaction, customerId, cancellationToken);

        await using (var command = new SqlCommand(
            "SELECT COUNT(*) FROM raw.DeletedCustomer WHERE CustomerId=@customerId;",
            sqlConnection, transaction))
        {
            command.Parameters.AddWithValue("@customerId", customerId);
            if ((int)(await command.ExecuteScalarAsync(cancellationToken))! > 0)
            {
                throw new InvalidOperationException(
                    $"Customer '{customerId}' is marked for deletion; customer-linked writes are not allowed.");
            }
        }

        await writeAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task DeleteCustomerDataAsync(
        SqlConnection sqlConnection,
        CosmosClient cosmosClient,
        string customerId,
        CancellationToken cancellationToken = default) =>
        await DeleteCustomerDataAsync(sqlConnection, cosmosClient, customerId, null, cancellationToken);

    internal static async Task DeleteCustomerDataAsync(
        SqlConnection sqlConnection,
        CosmosClient cosmosClient,
        string customerId,
        Func<CancellationToken, Task>? afterSqlCommitAsync,
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
        if (afterSqlCommitAsync is not null)
        {
            await afterSqlCommitAsync(cancellationToken);
        }
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
        await AcquireCustomerLifecycleLockAsync(connection, transaction, customerId, cancellationToken);
        await using var command = new SqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("@customerId", customerId);
        await command.ExecuteNonQueryAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public static async Task AcquireCustomerLifecycleLockAsync(
        SqlConnection connection,
        SqlTransaction transaction,
        string customerId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(connection);
        ArgumentNullException.ThrowIfNull(transaction);
        ArgumentException.ThrowIfNullOrWhiteSpace(customerId);
        if (!ReferenceEquals(transaction.Connection, connection))
            throw new ArgumentException("The transaction must belong to the supplied connection.", nameof(transaction));
        customerId = customerId.Trim().ToLowerInvariant();
        if (customerId.Length > 50)
            throw new ArgumentException("CustomerId cannot exceed 50 characters.", nameof(customerId));

        await using var command = new SqlCommand(
            """
            DECLARE @result INT;
            EXEC @result = sys.sp_getapplock
                @Resource = @resource,
                @LockMode = 'Exclusive',
                @LockOwner = 'Transaction',
                @LockTimeout = 30000;
            SELECT @result;
            """,
            connection,
            transaction);
        command.Parameters.AddWithValue("@resource", $"NICEDA:CustomerLifecycle:{customerId}");
        var result = (int)(await command.ExecuteScalarAsync(cancellationToken))!;
        if (result < 0)
        {
            throw new InvalidOperationException(
                $"Could not acquire the customer lifecycle lock for '{customerId}' (SQL result {result}).");
        }
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
