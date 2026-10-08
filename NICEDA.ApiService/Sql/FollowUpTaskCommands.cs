using Microsoft.Data.SqlClient;

namespace NICEDA.ApiService.Sql;

/// <summary>
/// Write side of the CQRS split (NICEDA-PRACTICE-8): a named command
/// (<see cref="CreateFollowUpTaskAsync"/>) with an explicit, validated
/// shape — not raw table/row access. This is the command the
/// <c>create_follow_up_task</c> MCP tool (a later slice) will call.
/// </summary>
public sealed record CreateFollowUpTaskCommand(string CustomerId, string Reason, DateOnly DueDate, string CreatedBy);

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
    {
        Validate(command);

        if (connection.State != System.Data.ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using (var customerCheck = new SqlCommand(
            "SELECT COUNT(*) FROM raw.Customer WHERE CustomerId = @id;", connection))
        {
            customerCheck.Parameters.AddWithValue("@id", command.CustomerId);
            if ((int)(await customerCheck.ExecuteScalarAsync(cancellationToken))! != 1)
            {
                throw new ArgumentException("Customer does not exist.", nameof(command));
            }
        }

        await using var sqlCommand = new SqlCommand(
            """
            INSERT INTO optimized.FollowUpTask (CustomerId, Reason, DueDate, CreatedBy, CreatedAtUtc)
            OUTPUT INSERTED.FollowUpTaskId
            VALUES (@customerId, @reason, @dueDate, @createdBy, @createdAtUtc);
            """,
            connection);
        sqlCommand.Parameters.AddWithValue("@customerId", command.CustomerId);
        sqlCommand.Parameters.AddWithValue("@reason", command.Reason);
        sqlCommand.Parameters.AddWithValue("@dueDate", command.DueDate.ToDateTime(TimeOnly.MinValue));
        sqlCommand.Parameters.AddWithValue("@createdBy", command.CreatedBy);
        sqlCommand.Parameters.AddWithValue("@createdAtUtc", DateTime.UtcNow);

        return (int)(await sqlCommand.ExecuteScalarAsync(cancellationToken))!;
    }

    public static void Validate(CreateFollowUpTaskCommand command)
    {
        if (string.IsNullOrWhiteSpace(command.CustomerId) || command.CustomerId.Length > 50)
            throw new ArgumentException("CustomerId must contain 1-50 characters.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.Reason) || command.Reason.Length > 500)
            throw new ArgumentException("Reason must contain 1-500 characters.", nameof(command));
        if (string.IsNullOrWhiteSpace(command.CreatedBy) || command.CreatedBy.Length > 200)
            throw new ArgumentException("CreatedBy must contain 1-200 characters.", nameof(command));
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
