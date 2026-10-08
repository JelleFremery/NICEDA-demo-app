namespace NICEDA.ApiService.Legal;

public sealed record LegalKnowledge(
    string SourceId, string CustomerId, string Title, string Concept,
    string Classification, string Owner, int RetentionDays, string SourcePath);

public static class LegalKnowledgeProjection
{
    // NICEDA-PRACTICE-4: a deterministic ingestion rule converts the mocked
    // folder hierarchy to explicit searchable governance metadata.
    public static LegalKnowledge Project(LegalDocument document)
    {
        var parts = document.FolderPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var customer = parts.Length > 1 && parts[0] == "Customers"
            ? parts[1].ToLowerInvariant()
            : "internal";
        var concept = document.FolderPath.Contains("/Compliance", StringComparison.Ordinal)
            ? "compliance"
            : customer == "internal" ? "policy" : "contract";
        return new(document.Id, customer, document.FileName, concept, "Restricted",
            "Legal", 2555, $"{document.FolderPath}/{document.FileName}");
    }
}
