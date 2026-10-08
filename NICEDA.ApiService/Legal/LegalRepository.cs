using System.Text.Json;

namespace NICEDA.ApiService.Legal;

/// <summary>
/// Mocked SharePoint ("Legal" repository): loads the seeded folder/file
/// listing from JSON. There is no real SharePoint here — it's a seeded
/// in-memory stand-in per the demo plan.
/// </summary>
public sealed class LegalRepository
{
    private readonly IReadOnlyList<LegalDocument> _documents;
    private readonly HashSet<string> _deleted = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public LegalRepository(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "Legal", "Seed", "legal-documents.json");
        var json = File.ReadAllText(path);
        _documents = JsonSerializer.Deserialize<IReadOnlyList<LegalDocument>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to load legal seed data from {path}.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<LegalDocument> GetAll()
    {
        lock (_gate) return _documents.Where(d =>
            !_deleted.Contains(LegalKnowledgeProjection.Project(d).CustomerId)).ToArray();
    }

    public void RemoveCustomer(string id)
    {
        lock (_gate) _deleted.Add(id);
    }
}
