using System.Text.Json.Nodes;
using Microsoft.Azure.Cosmos;

namespace NICEDA.ApiService.SupportDocs;

/// <summary>
/// Creates and seeds the "Before" Cosmos shape: the <c>raw-support</c>
/// container. Every document needs a <c>customerId</c> property only
/// because that's the physical partition key (a hard requirement) — beyond
/// that, field names, shapes, and vocabulary are deliberately inconsistent
/// between documents (ticketId vs. ticket_number vs. no id at all, no
/// classification/severity/region metadata anywhere). This is the "no
/// consistent structure or meaning, missing metadata" problem from the
/// demo brief. Contrast with the optimized-support container (later
/// slice), which carries a uniform metadata envelope (practice 4) and is
/// chunked by concept (practice 7).
/// </summary>
public sealed class RawSupportSeeder
{
    private const string DatabaseId = "niceda";
    private const string ContainerId = "raw-support";

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
        // Support ticket shape #1 — "ticketId", flat.
        yield return new JsonObject
        {
            ["id"] = "support-001",
            ["customerId"] = "betabit",
            ["ticketId"] = "TCK-4471",
            ["subject"] = "SSO login failures after IdP cert rotation",
            ["body"] = "Betabit reported SSO failures this morning after their IdP rotated its signing cert. Walked them through re-uploading the metadata. Resolved within 2 hours.",
            ["openedBy"] = "lena.moreau@betabit.example",
        };

        // Support ticket shape #2 — different field names, nested, severity
        // expressed as free text instead of a structured field.
        yield return new JsonObject
        {
            ["id"] = "support-002",
            ["customerId"] = "betabit",
            ["ticket_number"] = "ESC-991",
            ["details"] = new JsonObject
            {
                ["summary"] = "Escalation: renewal at risk, champion departed",
                ["raisedBy"] = "Account team",
            },
            ["note"] = "Escalated internally (not customer-facing) after sales flagged the Betabit renewal risk. Support has no action here but logging for visibility since support history is often the first thing reviewed before a renewal call.",
        };

        // Meeting notes — unstructured blob, no severity/classification at all.
        yield return new JsonObject
        {
            ["id"] = "support-003",
            ["customerId"] = "fenetre",
            ["type"] = "meeting-notes",
            ["text"] = "Quarterly check-in with Camille Laurent (Fenetre). Renewal went smoothly. She asked about the new reporting module. No open issues. Scheduled a Q2 follow-up to demo reporting.",
        };

        yield return new JsonObject
        {
            ["id"] = "support-004",
            ["customerId"] = "techorama",
            ["ticketId"] = "TCK-5102",
            ["subject"] = "Data export timing out before conference deadline",
            ["body"] = "Techorama needed a large data export ahead of a live event and it timed out twice. Increased their export timeout limit and reran successfully. They asked about multi-region support for future events.",
            ["openedBy"] = "jelle.vandamme@techorama.example",
        };

        // Security assessment — yet another shape, long free-text blob
        // mimicking a "50-page document" dumped as one field.
        yield return new JsonObject
        {
            ["id"] = "support-005",
            ["customerId"] = "contoso",
            ["docType"] = "security-assessment",
            ["fullText"] = "Annual security review for Contoso. Scope: platform access controls, data residency, incident response. Finding 1: access reviews performed quarterly, satisfactory. Finding 2: data residency confirmed NA region, satisfactory. Finding 3: incident response runbook reviewed, one recommendation to add a dedicated Contoso escalation contact. Overall: no high-severity findings. Contoso remains our heaviest support user by ticket volume but all within SLA.",
        };

        yield return new JsonObject
        {
            ["id"] = "support-006",
            ["customerId"] = "fabrikam",
            ["ticket_number"] = "ESC-1042",
            ["details"] = new JsonObject
            {
                ["summary"] = "Two tickets open past SLA, account going quiet",
                ["raisedBy"] = "Support lead",
            },
            ["note"] = "Fabrikam has two unresolved tickets past SLA. Combined with sales notes about a quiet account and a missed QBR, this account looks at-risk from support's side too.",
        };

        yield return new JsonObject
        {
            ["id"] = "support-007",
            ["customerId"] = "fabrikam",
            ["subject"] = "Billing question",
            ["body"] = "Fabrikam asked about invoice line items. Clarified and closed same day.",
        };

        yield return new JsonObject
        {
            ["id"] = "support-008",
            ["customerId"] = "contoso",
            ["ticketId"] = "TCK-3380",
            ["subject"] = "Routine API rate-limit question",
            ["body"] = "Answered a question about API rate limits. No action needed.",
            ["openedBy"] = "avery.brooks@contoso.example",
        };
    }
}
