using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using NICEDA.ApiService.Crm;
using NICEDA.ApiService.Legal;

namespace NICEDA.Tests;

/// <summary>
/// Minimal <see cref="IWebHostEnvironment"/> stand-in pointing at the real
/// NICEDA.ApiService project directory, so repository tests load the same
/// seed JSON the running app loads — no mocking framework needed for this.
/// </summary>
internal sealed class FakeWebHostEnvironment : IWebHostEnvironment
{
    public FakeWebHostEnvironment(string contentRootPath)
    {
        ContentRootPath = contentRootPath;
        WebRootPath = contentRootPath;
    }

    public string ApplicationName { get; set; } = "NICEDA.Tests";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; }
    public string EnvironmentName { get; set; } = "Development";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; }
}

public class SeedDataTests
{
    // Repository code reads Crm/Seed/*.json and Legal/Seed/*.json relative
    // to ContentRootPath; point at the ApiService project's own folder
    // (source, not build output) so the test doesn't depend on copy-to-output
    // timing.
    private static readonly string ApiServiceContentRoot = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NICEDA.ApiService"));

    private static CrmRepository CreateCrmRepository() =>
        new(new FakeWebHostEnvironment(ApiServiceContentRoot));

    private static LegalRepository CreateLegalRepository() =>
        new(new FakeWebHostEnvironment(ApiServiceContentRoot));

    [Fact]
    public void CrmSeed_ContainsRequiredCustomers()
    {
        var customers = CreateCrmRepository().GetAll();

        Assert.Equal(5, customers.Count);
        foreach (var expectedId in new[] { "betabit", "fenetre", "techorama" })
        {
            Assert.Contains(customers, c => c.Id == expectedId);
        }
    }

    [Fact]
    public void CrmSeed_CustomerFieldsAreNotBlankForRequiredCustomers()
    {
        var crm = CreateCrmRepository();

        foreach (var id in new[] { "betabit", "fenetre", "techorama" })
        {
            var customer = crm.GetById(id);
            Assert.NotNull(customer);
            Assert.False(string.IsNullOrWhiteSpace(customer!.CompanyName));
            Assert.False(string.IsNullOrWhiteSpace(customer.SalesNotes));
        }
    }

    [Fact]
    public void LegalSeed_HasDocumentsForRequiredCustomersByFolderPath()
    {
        var documents = CreateLegalRepository().GetAll();

        Assert.NotEmpty(documents);
        foreach (var customerFolder in new[] { "/Customers/Betabit", "/Customers/Fenetre", "/Customers/Techorama" })
        {
            Assert.Contains(documents, d => d.FolderPath.StartsWith(customerFolder, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void RawSqlSeeder_CustomerIdsMatchCrmSeedIds()
    {
        var crmIds = CreateCrmRepository().GetAll().Select(c => c.Id).ToHashSet();
        var sqlIds = NICEDA.ApiService.Sql.RawSqlSeeder.SeedCustomers.Select(c => c.Id).ToHashSet();

        Assert.Equal(crmIds, sqlIds);
    }

    [Fact]
    public void RawSupportSeeder_AllDocumentsHaveCustomerIdFromCrmSeed()
    {
        var crmIds = CreateCrmRepository().GetAll().Select(c => c.Id).ToHashSet();
        var documents = NICEDA.ApiService.SupportDocs.RawSupportSeeder.SeedDocuments().ToList();

        Assert.NotEmpty(documents);
        foreach (var document in documents)
        {
            var customerId = document["customerId"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(customerId));
            Assert.Contains(customerId, crmIds);
        }
    }

    [Fact]
    public void OptimizedSupportSeeder_AllDocumentsHaveClassificationMetadata()
    {
        var crmIds = CreateCrmRepository().GetAll().Select(c => c.Id).ToHashSet();
        var documents = NICEDA.ApiService.SupportDocs.OptimizedSupportSeeder.SeedDocuments().ToList();

        Assert.NotEmpty(documents);
        foreach (var document in documents)
        {
            var customerId = document["customerId"]?.GetValue<string>();
            Assert.False(string.IsNullOrWhiteSpace(customerId));
            Assert.Contains(customerId, crmIds);

            // NICEDA-PRACTICE-4: every optimized document must carry
            // classification metadata attached at ingestion time.
            Assert.False(string.IsNullOrWhiteSpace(document["classification"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(document["region"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(document["severity"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(document["concept"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(document["sourceSystem"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(document["ingestedAtUtc"]?.GetValue<string>()));
            var inventory = NICEDA.ApiService.DataInventory.DataInventoryCatalog.Entries.Single(e => e.Owner == "Support");
            Assert.Equal(inventory.Classification, document["classification"]!.GetValue<string>());
            Assert.Equal(inventory.Owner, document["owner"]!.GetValue<string>());
            Assert.Equal(730, document["retentionDays"]!.GetValue<int>());
            Assert.StartsWith("support-", document["sourceId"]!.GetValue<string>());
        }
    }

    [Fact]
    public void OptimizedSupportSeeder_SplitsContosoSecurityAssessmentIntoThreeConceptChunks()
    {
        var documents = NICEDA.ApiService.SupportDocs.OptimizedSupportSeeder.SeedDocuments().ToList();

        // NICEDA-PRACTICE-7: the single raw Contoso "security assessment"
        // blob must become 3 separate documents, each chunked around one
        // coherent concept (not an arbitrary character/token window).
        var chunks = documents
            .Where(d => d["customerId"]!.GetValue<string>() == "contoso"
                && d["concept"]!.GetValue<string>().StartsWith("security-finding-", StringComparison.Ordinal))
            .ToList();

        Assert.Equal(3, chunks.Count);
        Assert.Equal(3, chunks.Select(c => c["concept"]!.GetValue<string>()).Distinct().Count());
    }
}
