using Microsoft.Data.SqlClient;
using NICEDA.ApiService.Agent;
using NICEDA.ApiService.Security;

namespace NICEDA.ApiService.Audit;

public sealed record AgentUsageRow(
    string CustomerId,
    string CustomerName,
    string Status,
    int SearchKnowledge,
    int GetCustomerHealth,
    int CreateFollowUpTask,
    int Denied,
    int Failed,
    DateTime? LastAgentAccessUtc,
    string NeverUsedCapabilities);

/// <summary>
/// NICEDA-PRACTICE-10 (close the loop): "Which data do the agents access?
/// Which data is never touched?" Aggregates agent rows from the audit log
/// per customer and capability. Zero cells are the interesting part: data
/// the agent never needed (or could not find) is a candidate to improve or
/// to stop maintaining. A real system would aggregate per source document
/// and over a rolling window.
/// </summary>
public static class AgentUsage
{
    public const string NeverTouched = "Never touched by agent";
    public const string Used = "Used by agent";

    private static readonly string[] Capabilities =
    [
        AgentMcpContract.SearchKnowledgeToolName,
        AgentMcpContract.GetCustomerHealthToolName,
        AgentMcpContract.ToolName,
    ];

    public static async Task<IReadOnlyList<AgentUsageRow>> GetVisibleAsync(
        SqlConnection connection, DemoUser identity, CancellationToken cancellationToken = default)
    {
        var auditRows = await AgentActionAuditLog.GetVisibleAsync(connection, identity, cancellationToken);
        var customers = new List<(string Id, string Name)>();
        await using (var command = new SqlCommand(
            """
            SELECT CustomerId, CustomerName, Region FROM raw.Customer
            WHERE CustomerId NOT IN (SELECT CustomerId FROM raw.DeletedCustomer)
            ORDER BY CustomerName;
            """, connection))
        await using (var reader = await command.ExecuteReaderAsync(cancellationToken))
        {
            while (await reader.ReadAsync(cancellationToken))
            {
                // NICEDA-PRACTICE-5: usage statistics obey the same boundary.
                if (identity.CanAccess(reader.GetString(0), reader.GetString(2)))
                {
                    customers.Add((reader.GetString(0), reader.GetString(1)));
                }
            }
        }
        return Summarize(customers, auditRows);
    }

    internal static IReadOnlyList<AgentUsageRow> Summarize(
        IEnumerable<(string Id, string Name)> customers, IEnumerable<AuditLogRow> auditRows)
    {
        var agentRows = auditRows
            .Where(row => row.ActorType == "Agent" && row.CustomerId is not null)
            .ToLookup(row => row.CustomerId!, StringComparer.OrdinalIgnoreCase);

        return customers
            .Select(customer =>
            {
                var rows = agentRows[customer.Id].ToArray();
                int Count(string action) => rows.Count(row => row.Action == action);
                var neverUsed = Capabilities.Where(capability => Count(capability) == 0).ToArray();
                return new AgentUsageRow(
                    customer.Id,
                    customer.Name,
                    rows.Length == 0 ? NeverTouched : Used,
                    Count(AgentMcpContract.SearchKnowledgeToolName),
                    Count(AgentMcpContract.GetCustomerHealthToolName),
                    Count(AgentMcpContract.ToolName),
                    rows.Count(row => row.Outcome == "denied"),
                    rows.Count(row => row.Outcome == "failed"),
                    rows.Length == 0 ? null : rows.Max(row => row.OccurredAtUtc),
                    neverUsed.Length == 0 ? "—" : string.Join(", ", neverUsed));
            })
            .OrderBy(row => row.Status == NeverTouched ? 0 : 1)
            .ThenBy(row => row.SearchKnowledge + row.GetCustomerHealth + row.CreateFollowUpTask)
            .ToArray();
    }
}
