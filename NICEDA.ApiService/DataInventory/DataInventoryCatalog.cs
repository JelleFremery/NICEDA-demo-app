namespace NICEDA.ApiService.DataInventory;

/// <summary>
/// NICEDA-PRACTICE-1: Build a data inventory.
/// Literal, code-level data inventory so seeding and the data-explorer API
/// reference one source of truth instead of hardcoding system facts in
/// multiple places. Mirrors docs/data-inventory.md.
/// </summary>
public static class DataInventoryCatalog
{
    public static IReadOnlyList<DataInventoryEntry> Entries { get; } =
    [
        new DataInventoryEntry(
            System: "CRM",
            RealOrMocked: "Mocked (in-memory seed)",
            Contains: "Customer accounts, contacts, deal notes",
            Owner: "Sales",
            Classification: "Confidential",
            AgentPotentialBefore: "Low — key facts are buried in free-text notes across dozens of fields."),
        new DataInventoryEntry(
            System: "Azure SQL (raw)",
            RealOrMocked: "Real (SQL Server container)",
            Contains: "Contracts, contract lines, statuses",
            Owner: "Finance",
            Classification: "Restricted",
            AgentPotentialBefore: "Low — over-normalized; needs a multi-table join to answer a simple question."),
        new DataInventoryEntry(
            System: "Cosmos DB (raw-support)",
            RealOrMocked: "Real (Cosmos DB emulator container)",
            Contains: "Support tickets, meeting notes, security assessments",
            Owner: "Support",
            Classification: "Confidential",
            AgentPotentialBefore: "Low — inconsistent shape between documents, no metadata envelope."),
        new DataInventoryEntry(
            System: "SharePoint (Legal)",
            RealOrMocked: "Mocked (in-memory seed)",
            Contains: "Contracts (PDF stand-ins), compliance reports, policies",
            Owner: "Legal",
            Classification: "Restricted",
            AgentPotentialBefore: "Low — folder hierarchy is the only organizing principle, no metadata."),
    ];
}
