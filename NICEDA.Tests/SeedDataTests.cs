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
}
