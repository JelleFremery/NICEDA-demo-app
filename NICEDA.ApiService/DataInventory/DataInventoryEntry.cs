namespace NICEDA.ApiService.DataInventory;

/// <summary>
/// One row of the NICEDA data inventory (practice 1). Mirrors the table in
/// docs/data-inventory.md — keep both in sync when a system changes.
/// </summary>
public sealed record DataInventoryEntry(
    string System,
    string RealOrMocked,
    string Contains,
    string Owner,
    string Classification,
    string AgentPotentialBefore);
