using Microsoft.Data.SqlClient;
using System.Data;
using NICEDA.ApiService.DataLifecycle;

namespace NICEDA.ApiService.Sql;

/// <summary>
/// Write side of the CQRS split (NICEDA-PRACTICE-8): a named command
/// (<see cref="CreateFollowUpTaskAsync"/>) with an explicit, validated
/// shape — not raw table/row access. This is the command the
/// <c>create_follow_up_task</c> MCP tool (a later slice) will call.
/// </summary>
public sealed record CreateFollowUpTaskCommand(
    string CustomerId,
    string Reason,
    DateOnly DueDate,
    string CreatedBy,
    string? IdempotencyKey = null);

public sealed record FollowUpTaskCreationResult(int FollowUpTaskId, bool Created);

public sealed class IdempotencyKeyConflictException(string message) : InvalidOperationException(message);

public sealed record FollowUpTask(
    int FollowUpTaskId,
    string CustomerId,
    string Reason,
    DateTime DueDate,
    string CreatedBy,
    DateTime CreatedAtUtc,
    bool IsCompleted);

public static class FollowUpTaskCommands
{
    public static async Task<int> CreateFollowUpTaskAsync(
        SqlConnection connection, CreateFollowUpTaskCommand command, CancellationToken cancellationToken = default)
        => (await CreateFollowUpTaskWithResultAsync(connection, command, cancellationToken)).FollowUpTaskId;

    public static async Task<FollowUpTaskCreationResult> CreateFollowUpTaskWithResultAsync(
        SqlConnection connection, CreateFollowUpTaskCommand command, CancellationToken cancellationToken = default)
    {
        Validate(command);

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        using var transaction = connection.BeginTransaction(IsolationLevel.Serializable);
        await CustomerDeletionCommand.AcquireCustomerLifecycleLockAsync(
            connection,
            transaction,
            command.CustomerId.Trim().ToLowerInvariant(),
            cancellationToken);

        await using (var customerCheck = new SqlCommand(
            """
            SELECT COUNT(*)
            FROM raw.Customer c WITH (UPDLOCK, HOLDLOCK)
            WHERE c.CustomerId = @id
              AND NOT EXISTS (
                  SELECT 1
                  FROM raw.DeletedCustomer d WITH (UPDLOCK, HOLDLOCK)
                  WHERE d.CustomerId = c.CustomerId);
            """,
            connection,
            transaction))
        {
            customerCheck.Parameters.AddWithValue("@id", command.CustomerId);
            if ((int)(await customerCheck.ExecuteScalarAsync(cancellationToken))! != 1)
            {
                throw new ArgumentException("Customer does not exist or is marked for deletion.", nameof(command));
            }
        }

        if (command.IdempotencyKey is not null)
        {
            await using var existingCommand = new SqlCommand(
                """
                SELECT FollowUpTaskId, CustomerId, Reason, DueDate
                FROM optimized.FollowUpTask WITH (UPDLOCK, HOLDLOCK)
                WHERE CreatedBy = @createdBy AND IdempotencyKey = @idempotencyKey;
                """,
                connection,
                transaction);
            existingCommand.Parameters.AddWithValue("@createdBy", command.CreatedBy);
            existingCommand.Parameters.AddWithValue("@idempotencyKey", command.IdempotencyKey);
            await using var existingReader = await existingCommand.ExecuteReaderAsync(cancellationToken);
            if (await existingReader.ReadAsync(cancellationToken))
            {
                var existingId = existingReader.GetInt32(0);
                var samePayload =
                    string.Equals(existingReader.GetString(1), command.CustomerId, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(existingReader.GetString(2), command.Reason, StringComparison.Ordinal) &&
                    DateOnly.FromDateTime(existingReader.GetDateTime(3)) == command.DueDate;
                await existingReader.DisposeAsync();
                if (!samePayload)
                {
                    throw new IdempotencyKeyConflictException(
                        "This idempotency key has already been used for a different follow-up task payload.");
                }
                transaction.Commit();
                return new FollowUpTaskCreationResult(existingId, Created: false);
            }
        }

        await using var sqlCommand = new SqlCommand(
            """
            INSERT INTO optimized.FollowUpTask (CustomerId, Reason, DueDate, CreatedBy, IdempotencyKey, CreatedAtUtc)
            OUTPUT INSERTED.FollowUpTaskId
            VALUES (@customerId, @reason, @dueDate, @createdBy, @idempotencyKey, @createdAtUtc);
            """,
            connection,
            transaction);
        sqlCommand.Parameters.AddWithValue("@customerId", command.CustomerId);
        sqlCommand.Parameters.AddWithValue("@reason", command.Reason);
        sqlCommand.Parameters.AddWithValue("@dueDate", command.DueDate.ToDateTime(TimeOnly.MinValue));
        sqlCommand.Parameters.AddWithValue("@createdBy", command.CreatedBy);
        sqlCommand.Parameters.AddWithValue("@idempotencyKey", (object?)command.IdempotencyKey ?? DBNull.Value);
        sqlCommand.Parameters.AddWithValue("@createdAtUtc", DateTime.UtcNow);

        var taskId = (int)(await sqlCommand.ExecuteScalarAsync(cancellationToken))!;
        transaction.Commit();
        return new FollowUpTaskCreationResult(taskId, Created: true);
    }

    public static void Validate(CreateFollowUpTaskCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerId) || command.CustomerId.Length > 50)
            throw new ArgumentException("CustomerId must contain 1-50 characters.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500)
            throw new ArgumentException("Reason must contain 1-500 characters.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.CreatedBy) || command.CreatedBy.Length > 200)
            throw new ArgumentException("CreatedBy must contain 1-200 characters.", nameof(command));
        if (command.IdempotencyKey is { } key && (string.IsNullOrWhiteSpace(key) || key.Length > 100))
            throw new ArgumentException("IdempotencyKey must contain 1-100 characters when supplied.", nameof(command));
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        if (command.DueDate < today || command.DueDate > today.AddYears(1))
            throw new ArgumentException("DueDate must be today or within the next year.", nameof(command));
    }

    public static async Task<List<FollowUpTask>> GetFollowUpTasksAsync(
        SqlConnection connection, string? customerId = null, CancellationToken cancellationToken = default)
    {
        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var query = "SELECT FollowUpTaskId, CustomerId, Reason, DueDate, CreatedBy, CreatedAtUtc, IsCompleted FROM optimized.FollowUpTask";
        if (customerId is not null)
        {
            query += " WHERE CustomerId = @customerId";
        }
        query += " ORDER BY CreatedAtUtc DESC;";

        await using var sqlCommand = new SqlCommand(query, connection);
        if (customerId is not null)
        {
            sqlCommand.Parameters.AddWithValue("@customerId", customerId);
        }

        await using var reader = await sqlCommand.ExecuteReaderAsync(cancellationToken);
        var results = new List<FollowUpTask>();
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new FollowUpTask(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetDateTime(3),
                reader.GetString(4),
                reader.GetDateTime(5),
                reader.GetBoolean(6)));
        }
        return results;
    }
}
