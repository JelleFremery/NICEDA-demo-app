using NICEDA.ApiService.Crm;
using NICEDA.ApiService.Legal;
using NICEDA.ApiService.DataInventory;

namespace NICEDA.Tests;

public class SemanticProjectionTests
{
    private static readonly string Root = Path.GetFullPath(
        Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "NICEDA.ApiService"));

    [Theory]
    [InlineData("betabit", "Betabit")]
    [InlineData("fenetre", "Fenetre")]
    [InlineData("techorama", "Techorama")]
    public void ProfilesRetainCustomerAndEvidence(string id, string name)
    {
        var raw = new CrmRepository(new FakeWebHostEnvironment(Root)).GetById(id)!;
        var profile = CustomerProfiles.Project(raw);
        Assert.Equal(name, profile.CustomerName);
        Assert.Equal(id, profile.SourceId);
        Assert.Contains(raw.SalesNotes, profile.HealthSummary);
        Assert.Equal(raw.RenewalDateText, profile.RenewalContext);
    }

    [Fact]
    public void LegalProjectionHasMetadataAndProvenance()
    {
        var raw = new LegalRepository(new FakeWebHostEnvironment(Root)).GetAll();
        var projected = raw.Select(LegalKnowledgeProjection.Project).ToArray();
        Assert.Equal(raw.Count, projected.Length);
        Assert.Contains(projected, d => d.CustomerId == "betabit" && d.Concept == "compliance");
        Assert.All(projected, d =>
        {
            Assert.Equal("Restricted", d.Classification);
            Assert.Equal("Legal", d.Owner);
            Assert.True(d.RetentionDays > 0);
            Assert.EndsWith(".pdf", d.SourcePath);
        });
        Assert.Contains("RiskLevel", SemanticGlossary.Terms.Keys);
    }

    [Fact]
    public void DeletedCustomerIsRemovedFromBothMockedSystems()
    {
        var environment = new FakeWebHostEnvironment(Root);
        var crm = new CrmRepository(environment);
        var legal = new LegalRepository(environment);
        crm.RemoveCustomer("betabit");
        legal.RemoveCustomer("betabit");
        Assert.Null(crm.GetById("betabit"));
        Assert.DoesNotContain(legal.GetAll(), d => LegalKnowledgeProjection.Project(d).CustomerId == "betabit");
        Assert.NotNull(crm.GetById("fenetre"));
    }
}
