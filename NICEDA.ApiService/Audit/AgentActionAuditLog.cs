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

    // Dummy history so the Audit log tab is populated on a fresh run: human
    // and system activity plus earlier agent sessions in both Before and
    // After mode. Each row carries a stable "seed-NN" InteractionId so
    // seeding is idempotent per row, even on an already-populated table.
    internal static readonly (int DaysAgo, AuditEntry Entry)[] SeedEntries =
    [
        (30, new("System", "crm-sync", null, null, "sync_crm_accounts", "Write", null, "succeeded", "Nightly CRM account sync", "seed-01")),
        (28, new("Human", "jane.okafor@niceda.example", null, null, "view_customer", "Read", "betabit", "succeeded", "Opened account overview", "seed-02")),
        (26, new("Agent", AgentActor, "jane", "Before", "search_knowledge", "Read", "betabit", "succeeded", "4 knowledge hit(s): raw ticket text, no concept or classification", "seed-03")),
        (26, new("Agent", AgentActor, "jane", "Before", "search_knowledge", "Read", "fenetre", "succeeded", "2 knowledge hit(s): raw-sup-003, raw-sup-004", "seed-04")),
        (24, new("System", "support-import", null, null, "import_support_tickets", "Write", "contoso", "succeeded", "Imported 2 support tickets", "seed-05")),
        (21, new("Human", "jane.okafor@niceda.example", null, null, "update_contract", "Write", "betabit", "succeeded", "Logged renewal risk after QBR", "seed-06")),
        (20, new("Agent", AgentActor, "admin", "Before", "search_knowledge", "Read", "fabrikam", "succeeded", "1 knowledge hit(s): contract text chunked mid-clause", "seed-07")),
        (20, new("Agent", AgentActor, "admin", "Before", "create_follow_up_task", "Write", "fabrikam", "failed", "Follow-up tasks can only be created in After mode.", "seed-08")),
        (18, new("Human", "marcus.lindqvist@niceda.example", null, null, "view_customer", "Read", "contoso", "succeeded", "Opened account overview", "seed-09")),
        (16, new("System", "crm-sync", null, null, "sync_crm_accounts", "Write", null, "failed", "CRM API timeout; retried next night", "seed-10")),
        (14, new("Agent", AgentActor, "jane", "After", "search_knowledge", "Read", "betabit", "succeeded", "3 knowledge hit(s), classified Internal", "seed-11")),
        (14, new("Agent", AgentActor, "jane", "After", "get_customer_health", "Read", "betabit", "succeeded", "Read customer health record", "seed-12")),
        (14, new("Agent", AgentActor, "jane", "After", "create_follow_up_task", "Write", "betabit", "created", "Task 3001 via MCP", "seed-13")),
        (12, new("Human", "marcus.lindqvist@niceda.example", null, null, "create_follow_up_task", "Write", "fabrikam", "succeeded", "Manual escalation call", "seed-14")),
        (10, new("Agent", AgentActor, "jane", "After", "get_customer_health", "Read", "contoso", "denied", "Identity not authorized for this customer", "seed-15")),
        (9, new("System", "support-import", null, null, "import_support_tickets", "Write", "fabrikam", "succeeded", "Imported 3 support tickets", "seed-16")),
        (8, new("Agent", AgentActor, "admin", "After", "get_customer_health", "Read", "contoso", "succeeded", "Read customer health record", "seed-17")),
        (8, new("Agent", AgentActor, "admin", "After", "create_follow_up_task", "Write", "contoso", "existing", "Idempotent retry returned existing task 3002", "seed-18")),
        (6, new("Human", "jane.okafor@niceda.example", null, null, "create_follow_up_task", "Write", "fenetre", "succeeded", "Manual renewal check-in", "seed-19")),
        (5, new("Agent", AgentActor, "jane", "Before", "search_knowledge", "Read", "betabit", "succeeded", "Raw search returned duplicate, unclassified notes", "seed-20")),
        (4, new("Agent", AgentActor, "jane", "After", "search_knowledge", "Read", "fenetre", "succeeded", "2 knowledge hit(s), classified Confidential", "seed-21")),
        (3, new("Human", "admin", null, null, "export_report", "Read", "techorama", "succeeded", "Quarterly account report", "seed-22")),
        (2, new("Agent", AgentActor, "jane", "After", "search_knowledge", "Read", null, "succeeded", "0 knowledge hits", "seed-23")),
        (1, new("System", "crm-sync", null, null, "sync_crm_accounts", "Write", null, "succeeded", "Nightly CRM account sync", "seed-24")),
    ];

    public static async Task EnsureCreatedAndSeededAsync(
        SqlConnection connection, IReadOnlySet<string> existingCustomerIds, CancellationToken cancellationToken = default)
    {
        await OpenAsync(connection, cancellationToken);
        await using (var schema = new SqlCommand(SchemaSql, connection))
        {
            await schema.ExecuteNonQueryAsync(cancellationToken);
        }

        // Replace the earlier unkeyed seed (only seeds write non-agent rows).
        await using (var legacy = new SqlCommand(
            "DELETE FROM audit.AgentActionLog WHERE InteractionId IS NULL AND ActorType <> 'Agent';", connection))
        {
            await legacy.ExecuteNonQueryAsync(cancellationToken);
        }

        // NICEDA-PRACTICE-6: never seed audit rows for an erased customer.
        // Insert missing seed rows and refresh existing ones, so existing
        // databases pick up seed corrections too.
        foreach (var (daysAgo, entry) in SeedEntriesFor(existingCustomerIds))
        {
            await using var refresh = new SqlCommand(
                "UPDATE audit.AgentActionLog SET CustomerId = @customerId, Detail = @detail, Outcome = @outcome WHERE InteractionId = @id;", connection);
            refresh.Parameters.AddWithValue("@id", entry.InteractionId);
            refresh.Parameters.AddWithValue("@customerId", (object?)entry.CustomerId ?? DBNull.Value);
            refresh.Parameters.AddWithValue("@detail", (object?)entry.Detail ?? DBNull.Value);
            refresh.Parameters.AddWithValue("@outcome", entry.Outcome);
            if (await refresh.ExecuteNonQueryAsync(cancellationToken) > 0) continue;
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
