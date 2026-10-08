using System.Text.Json;

namespace NICEDA.ApiService.Crm;

/// <summary>
/// Mocked CRM: loads the seeded "Before" customer data from JSON into
/// memory at startup. There is no real CRM product here — per the demo
/// plan, CRM is intentionally mocked while SQL/Cosmos are real containers.
/// </summary>
public sealed class CrmRepository
{
    private readonly IReadOnlyList<CrmCustomer> _customers;

    public CrmRepository(IWebHostEnvironment environment)
    {
        var path = Path.Combine(environment.ContentRootPath, "Crm", "Seed", "crm-customers.json");
        var json = File.ReadAllText(path);
        _customers = JsonSerializer.Deserialize<IReadOnlyList<CrmCustomer>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to load CRM seed data from {path}.");
    }

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public IReadOnlyList<CrmCustomer> GetAll() => _customers;

    public CrmCustomer? GetById(string id) =>
        _customers.FirstOrDefault(c => string.Equals(c.Id, id, StringComparison.OrdinalIgnoreCase));
}
