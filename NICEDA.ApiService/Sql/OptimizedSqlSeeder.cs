using System.Data;
using Microsoft.Data.SqlClient;

namespace NICEDA.ApiService.Sql;

/// <summary>
/// Creates the "After" SQL shape: a thin <c>optimized</c> schema layered
/// over the exact same <c>raw</c> rows (no duplicated data — see
/// docs/demo-briefing.md §4). This is where several practices become
/// concrete:
///
/// NICEDA-PRACTICE-3 (add a semantic layer): <c>CustomerHealthView</c>
/// replaces the 3-table join an agent would otherwise have to
/// reverse-engineer with one business-named projection
/// (CustomerId/Name/Region/AnnualValue/RiskLevel/RenewalInDays).
///
/// NICEDA-PRACTICE-8 (apply CQRS): the view above is the *read* side.
/// <c>optimized.FollowUpTask</c> plus <see cref="FollowUpTaskCommands"/>
/// is the *write* side — the agent never gets raw table access, only a
/// named command (<c>CreateFollowUpTask</c>) and a named query
/// (<c>GetCustomerHealth</c>).
///
/// NICEDA-PRACTICE-6: the CustomerDeletionCommand removes customer-owned
/// rows including follow-up tasks; its integration check uses scratch data.
/// </summary>
public sealed class OptimizedSqlSeeder
{
    // NICEDA-PRACTICE-3: one business-named view instead of forcing every
    // caller (human or agent) to know that "risk" lives in a join across
    // Customer/Contract/Status and a date comparison against EndDate.
    // CREATE/ALTER VIEW must be the only statement in its batch, which is
    // naturally true here since each array entry runs as its own
    // SqlCommand (its own batch).
    private const string CreateOrAlterCustomerHealthView = """
        CREATE OR ALTER VIEW optimized.CustomerHealthView AS
        SELECT
            c.CustomerId,
            c.CustomerName,
            c.Region,
            s.StatusCode,
            k.AnnualValue,
            k.EndDate,
            DATEDIFF(day, GETUTCDATE(), k.EndDate) AS RenewalInDays,
            CASE
                WHEN s.StatusCode = 'AtRisk' THEN 'High'
                WHEN DATEDIFF(day, GETUTCDATE(), k.EndDate) < 90 THEN 'Medium'
                ELSE 'Low'
            END AS RiskLevel
        FROM raw.Customer c
        JOIN raw.Contract k ON k.CustomerId = c.CustomerId
        JOIN raw.Status s ON s.StatusId = k.StatusId;
        """;

    private static readonly string[] SchemaStatements =
    [
        "IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = 'optimized') EXEC('CREATE SCHEMA optimized');",
        CreateOrAlterCustomerHealthView,
        // NICEDA-PRACTICE-8 (write side): a named command target, not a
        // generic table the agent could run arbitrary writes against.
        """
        IF OBJECT_ID('optimized.FollowUpTask', 'U') IS NULL
        CREATE TABLE optimized.FollowUpTask (
            FollowUpTaskId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
            CustomerId NVARCHAR(50) NOT NULL,
            Reason NVARCHAR(500) NOT NULL,
            DueDate DATE NOT NULL,
            CreatedBy NVARCHAR(200) NOT NULL,
            IdempotencyKey NVARCHAR(100) NULL,
            CreatedAtUtc DATETIME2 NOT NULL,
            IsCompleted BIT NOT NULL DEFAULT 0
        );
        """,
        """
        IF COL_LENGTH('optimized.FollowUpTask', 'IdempotencyKey') IS NULL
            ALTER TABLE optimized.FollowUpTask ADD IdempotencyKey NVARCHAR(100) NULL;
        """,
        """
        IF NOT EXISTS (SELECT 1 FROM sys.indexes
            WHERE name = 'UX_FollowUpTask_CreatedBy_IdempotencyKey'
              AND object_id = OBJECT_ID('optimized.FollowUpTask'))
            CREATE UNIQUE INDEX UX_FollowUpTask_CreatedBy_IdempotencyKey
                ON optimized.FollowUpTask (CreatedBy, IdempotencyKey)
                WHERE IdempotencyKey IS NOT NULL;
        """,
    ];

    public async Task EnsureCreatedAsync(SqlConnection connection, CancellationToken cancellationToken = default)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        foreach (var statement in SchemaStatements)
        {
            await using var command = new SqlCommand(statement, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
