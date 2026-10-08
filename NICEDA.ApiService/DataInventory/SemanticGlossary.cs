namespace NICEDA.ApiService.DataInventory;

public static class SemanticGlossary
{
    // NICEDA-PRACTICE-3: shared vocabulary explicitly defines the view fields.
    public static IReadOnlyDictionary<string, string> Terms { get; } =
        new Dictionary<string, string>
        {
            ["CustomerId"] = "Canonical account key linking CRM, contracts, support and legal.",
            ["RiskLevel"] = "High: contract status AtRisk; Medium: renewal within 90 days; otherwise Low. Review CRM/support evidence as well.",
            ["RenewalInDays"] = "UTC days until the contract end date; negative means overdue.",
            ["HealthSummary"] = "Curated sales and support narrative, retaining CRM provenance.",
            ["Concept"] = "Business topic used to split and retrieve meaningful knowledge chunks.",
        };
}
