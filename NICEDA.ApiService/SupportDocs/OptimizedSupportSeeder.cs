using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;

namespace NICEDA.ApiService.SupportDocs;

/// <summary>
/// Creates and seeds the "After" Cosmos shape: the <c>optimized-support</c>
/// container. Every document reshapes the same underlying support history
/// as <see cref="RawSupportSeeder"/>, but with two practices applied:
///
/// NICEDA-PRACTICE-4 (classify during ingestion): every document carries a
/// uniform metadata envelope — <c>classification</c>, <c>region</c>,
/// <c>severity</c>, <c>concept</c>, <c>sourceSystem</c> — stamped here, at
/// write time, not inferred later by an agent at query time.
///
/// NICEDA-PRACTICE-7 (chunk based on meaning): the raw Contoso
/// "security-assessment" document bundled three unrelated findings
/// (access controls, data residency, incident response) into one 300-word
/// blob. Here it's split into three documents, one per concept, so a
/// retrieval query about "data residency" doesn't drag in unrelated
/// findings as noise — contrast with raw-support's single-blob shape.
/// </summary>
public sealed class OptimizedSupportSeeder
{
    private const string DatabaseId = "niceda";
    private const string ContainerId = "optimized-support";

    public async Task EnsureSeededAsync(CosmosClient client, IReadOnlySet<string> customerIds, CancellationToken cancellationToken = default)
    {
        var database = await client.CreateDatabaseIfNotExistsAsync(DatabaseId, cancellationToken: cancellationToken);
        var containerResponse = await database.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(ContainerId, "/customerId"),
            cancellationToken: cancellationToken);
        var container = containerResponse.Container;

        foreach (var document in SeedDocuments())
        {
            if (!customerIds.Contains(document["customerId"]!.GetValue<string>())) continue;
            await container.UpsertItemAsync(
                document,
                new PartitionKey(document["customerId"]!.GetValue<string>()),
                cancellationToken: cancellationToken);
        }
    }

    internal static IEnumerable<JsonObject> SeedDocuments()
    {
        var ingestedAt = new DateTime(2026, 10, 8, 0, 0, 0, DateTimeKind.Utc);

        JsonObject Envelope(string id, string customerId, string concept, string region, string severity, string summary) =>
            new()
            {
                ["id"] = id,
                ["customerId"] = customerId,
                ["sourceSystem"] = "support",
                ["sourceId"] = id.Replace("optimized-sup-", "support-", StringComparison.Ordinal).TrimEnd('a', 'b', 'c'),
                ["owner"] = "Support",
                ["retentionDays"] = 730,
                ["concept"] = concept,
                ["classification"] = "Confidential",
                ["region"] = region,
                ["severity"] = severity,
                ["summary"] = summary,
                ["ingestedAtUtc"] = ingestedAt.ToString("O"),
            };

        // Mirrors raw support-001.
        yield return Envelope(
            "optimized-sup-001", "betabit", "incident-resolution", "EU", "Medium",
            "Betabit SSO login failures after an IdP certificate rotation; resolved within 2 hours by re-uploading IdP metadata.");

        // Mirrors raw support-002.
        yield return Envelope(
            "optimized-sup-002", "betabit", "renewal-risk-escalation", "EU", "High",
            "Betabit renewal at risk: the customer's champion departed and procurement is requesting a competing quote ahead of renewal.");

        // Mirrors raw support-003.
        yield return Envelope(
            "optimized-sup-003", "fenetre", "meeting-notes", "EU", "Low",
            "Fenetre quarterly check-in: renewal went smoothly, no open issues, customer interested in the new reporting module.");

        // Mirrors raw support-004.
        yield return Envelope(
            "optimized-sup-004", "techorama", "incident-resolution", "EU", "Medium",
            "Techorama data export timed out twice ahead of a conference deadline; resolved by raising the export timeout limit.");

        // Mirrors raw support-005 (Contoso security assessment) — split
        // into three concept chunks instead of one blob.
        yield return Envelope(
            "optimized-sup-005a", "contoso", "security-finding-access-controls", "NA", "Low",
            "Contoso annual security review — access controls: quarterly access reviews are performed and satisfactory.");
        yield return Envelope(
            "optimized-sup-005b", "contoso", "security-finding-data-residency", "NA", "Low",
            "Contoso annual security review — data residency: confirmed data resides in the NA region as required, satisfactory.");
        yield return Envelope(
            "optimized-sup-005c", "contoso", "security-finding-incident-response", "NA", "Medium",
            "Contoso annual security review — incident response: runbook reviewed; recommendation to add a dedicated Contoso escalation contact.");

        // Mirrors raw support-006.
        yield return Envelope(
            "optimized-sup-006", "fabrikam", "renewal-risk-escalation", "NA", "High",
            "Fabrikam has two support tickets open past SLA and the account has gone quiet; looks at-risk from the support side too.");

        // Mirrors raw support-007.
        yield return Envelope(
            "optimized-sup-007", "fabrikam", "billing", "NA", "Low",
            "Fabrikam billing question about invoice line items; clarified and closed the same day.");

        // Mirrors raw support-008.
        yield return Envelope(
            "optimized-sup-008", "contoso", "billing", "NA", "Low",
            "Contoso routine question about API rate limits; answered, no action needed.");
    }
}
