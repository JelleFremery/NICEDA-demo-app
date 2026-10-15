using System.Data;
using Microsoft.Data.SqlClient;
using NICEDA.ApiService.Security;

namespace NICEDA.ApiService.Audit;

public sealed record AuditEntry(
    string ActorType,
    string Actor,
    string? OnBehalfOf,
    string? Mode,
    string Action,
    string ActionKind,
    string? CustomerId,
    string Outcome,
    string? Detail,
    string? InteractionId = null);

public sealed record AuditLogRow(
    long AuditId,
    DateTime OccurredAtUtc,
    string ActorType,
    string Actor,
    string? OnBehalfOf,
    string? Mode,
    string Action,
    string ActionKind,
    string? CustomerId,
    string Outcome,
    string? Detail,
    string? InteractionId);

/// <summary>
/// NICEDA-PRACTICE-10 (observability): one SQL audit trail where human,
/// system and agent actions sit side by side, so the audience can see
/// exactly which tools the agent called, for whom, in which mode, and with
/// what outcome. It lives in SQL, not Cosmos, because it is an append-only
/// operational ledger queried by customer and time. A production audit log
/// would also need tamper evidence and a retention policy.
/// </summary>
public sealed class AgentActionAuditLog(IServiceScopeFactory scopeFactory, ILogger<AgentActionAuditLog> logger)
{
    public const string AgentActor = "niceda-agent";
    private const int MaxDetailLength = 500;

    private const string SchemaSql = """
        IF SCHEMA_ID('audit') IS NULL EXEC('CREATE SCHEMA audit');
        IF OBJECT_ID('audit.AgentActionLog', 'U') IS NULL
        CREATE TABLE audit.AgentActionLog (
            AuditId BIGINT IDENTITY(1,1) PRIMARY KEY,
            OccurredAtUtc DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
            ActorType NVARCHAR(20) NOT NULL,
            Actor NVARCHAR(100) NOT NULL,
            OnBehalfOf NVARCHAR(100) NULL,
            Mode NVARCHAR(10) NULL,
            Action NVARCHAR(100) NOT NULL,
            ActionKind NVARCHAR(10) NOT NULL,
            CustomerId NVARCHAR(50) NULL,
            Outcome NVARCHAR(20) NOT NULL,
            Detail NVARCHAR(500) NULL,
            InteractionId NVARCHAR(50) NULL
        );
        """;

    // Historical human/system activity so agent rows appear in context.
    internal static readonly (int DaysAgo, AuditEntry Entry)[] SeedEntries =
    [
        (21, new("System", "crm-sync", null, null, "sync_crm_accounts", "Write", null, "succeeded", "Nightly CRM account sync")),
        (14, new("Human", "jane.okafor@niceda.example", null, null, "update_contract", "Write", "betabit", "succeeded", "Logged renewal risk after QBR")),
        (12, new("Human", "marcus.lindqvist@niceda.example", null, null, "view_customer", "Read", "contoso", "succeeded", "Opened account overview")),
        (9, new("System", "support-import", null, null, "import_support_tickets", "Write", "fabrikam", "succeeded", "Imported 3 support tickets")),
        (6, new("Human", "jane.okafor@niceda.example", null, null, "create_follow_up_task", "Write", "fenetre", "succeeded", "Manual renewal check-in")),
        (3, new("Human", "admin", null, null, "export_report", "Read", "techorama", "succeeded", "Quarterly account report")),
        (1, new("System", "crm-sync", null, null, "sync_crm_accounts", "Write", null, "succeeded", "Nightly CRM account sync")),
    ];

    public static async Task EnsureCreatedAndSeededAsync(
        SqlConnection connection, IReadOnlySet<string> existingCustomerIds, CancellationToken cancellationToken = default)
    {
        await OpenAsync(connection, cancellationToken);
        await using (var schema = new SqlCommand(SchemaSql, connection))
        {
            await schema.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var count = new SqlCommand("SELECT COUNT(*) FROM audit.AgentActionLog;", connection))
        {
            if ((int)(await count.ExecuteScalarAsync(cancellationToken))! > 0) return;
        }

        // NICEDA-PRACTICE-6: never seed audit rows for an erased customer.
        foreach (var (daysAgo, entry) in SeedEntriesFor(existingCustomerIds))
        {
            await InsertAsync(connection, entry, DateTime.UtcNow.AddDays(-daysAgo), cancellationToken);
        }
    }

    internal static IEnumerable<(int DaysAgo, AuditEntry Entry)> SeedEntriesFor(IReadOnlySet<string> existingCustomerIds) =>
        SeedEntries.Where(seed => seed.Entry.CustomerId is null || existingCustomerIds.Contains(seed.Entry.CustomerId));

    // Audit failures are logged but never break the agent's answer.
    public async Task RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var connection = scope.ServiceProvider.GetRequiredService<SqlConnection>();
            await OpenAsync(connection, cancellationToken);
            await InsertAsync(connection, entry, null, cancellationToken);
        }
        catch (Exception exception) when (exception is SqlException or InvalidOperationException)
        {
            logger.LogWarning(exception, "Could not write audit row for {Action}", entry.Action);
        }
    }

    public static async Task<IReadOnlyList<AuditLogRow>> GetVisibleAsync(
        SqlConnection connection, DemoUser identity, CancellationToken cancellationToken = default)
    {
        await OpenAsync(connection, cancellationToken);
        await using var command = new SqlCommand(
            """
            SELECT TOP (500) AuditId, OccurredAtUtc, ActorType, Actor, OnBehalfOf, Mode, Action,
                   ActionKind, CustomerId, Outcome, Detail, InteractionId
            FROM audit.AgentActionLog
            ORDER BY OccurredAtUtc DESC, AuditId DESC;
            """, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var rows = new List<AuditLogRow>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var row = new AuditLogRow(
                reader.GetInt64(0), reader.GetDateTime(1), reader.GetString(2), reader.GetString(3),
                NullableString(reader, 4), NullableString(reader, 5), reader.GetString(6), reader.GetString(7),
                NullableString(reader, 8), reader.GetString(9), NullableString(reader, 10), NullableString(reader, 11));
            if (IsVisibleTo(row, identity)) rows.Add(row);
        }
        return rows;
    }

    // NICEDA-PRACTICE-5: the audit trail obeys the same permission boundary
    // as the data it describes.
    internal static bool IsVisibleTo(AuditLogRow row, DemoUser identity) =>
        identity.IsAdministrator
        || (row.CustomerId is not null
            ? identity.CanAccess(row.CustomerId, null)
            : row.ActorType == "System"
              || string.Equals(row.OnBehalfOf, identity.UserId, StringComparison.OrdinalIgnoreCase)
              || string.Equals(row.Actor, identity.UserId, StringComparison.OrdinalIgnoreCase));

    private static async Task InsertAsync(
        SqlConnection connection, AuditEntry entry, DateTime? occurredAtUtc, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            """
            INSERT INTO audit.AgentActionLog
                (OccurredAtUtc, ActorType, Actor, OnBehalfOf, Mode, Action, ActionKind, CustomerId, Outcome, Detail, InteractionId)
            VALUES (COALESCE(@occurredAtUtc, SYSUTCDATETIME()), @actorType, @actor, @onBehalfOf, @mode, @action,
                    @actionKind, @customerId, @outcome, @detail, @interactionId);
            """, connection);
        command.Parameters.Add("@occurredAtUtc", SqlDbType.DateTime2).Value = (object?)occurredAtUtc ?? DBNull.Value;
        command.Parameters.AddWithValue("@actorType", entry.ActorType);
        command.Parameters.AddWithValue("@actor", entry.Actor);
        command.Parameters.AddWithValue("@onBehalfOf", (object?)entry.OnBehalfOf ?? DBNull.Value);
        command.Parameters.AddWithValue("@mode", (object?)entry.Mode ?? DBNull.Value);
        command.Parameters.AddWithValue("@action", entry.Action);
        command.Parameters.AddWithValue("@actionKind", entry.ActionKind);
        command.Parameters.AddWithValue("@customerId", (object?)entry.CustomerId?.ToLowerInvariant() ?? DBNull.Value);
        command.Parameters.AddWithValue("@outcome", entry.Outcome);
        command.Parameters.AddWithValue("@detail", (object?)Truncate(entry.Detail) ?? DBNull.Value);
        command.Parameters.AddWithValue("@interactionId", (object?)entry.InteractionId ?? DBNull.Value);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    internal static string? Truncate(string? detail) =>
        detail is { Length: > MaxDetailLength } ? detail[..MaxDetailLength] : detail;

    private static string? NullableString(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);

    private static async Task OpenAsync(SqlConnection connection, CancellationToken cancellationToken)
    {
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }
    }
}
