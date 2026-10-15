using NICEDA.ApiService.Audit;
using NICEDA.ApiService.Security;

namespace NICEDA.Tests;

public sealed class AuditLogTests
{
    private static AuditLogRow Row(string actorType, string actor, string? onBehalfOf, string? customerId) =>
        new(1, DateTime.UtcNow, actorType, actor, onBehalfOf, "After", "search_knowledge", "Read",
            customerId, "succeeded", null, null);

    [Fact]
    public void Jane_SeesOnlyAuthorizedCustomersAndHerOwnUnscopedAgentRows()
    {
        var jane = DemoIdentity.Resolve("jane");

        Assert.True(AgentActionAuditLog.IsVisibleTo(Row("Agent", AgentActionAuditLog.AgentActor, "jane", "betabit"), jane));
        Assert.False(AgentActionAuditLog.IsVisibleTo(Row("Agent", AgentActionAuditLog.AgentActor, "jane", "contoso"), jane));
        Assert.True(AgentActionAuditLog.IsVisibleTo(Row("Agent", AgentActionAuditLog.AgentActor, "jane", null), jane));
        Assert.False(AgentActionAuditLog.IsVisibleTo(Row("Agent", AgentActionAuditLog.AgentActor, "admin", null), jane));
        Assert.True(AgentActionAuditLog.IsVisibleTo(Row("System", "crm-sync", null, null), jane));
    }

    [Fact]
    public void Admin_SeesEveryRow()
    {
        var admin = DemoIdentity.Resolve("admin");

        Assert.True(AgentActionAuditLog.IsVisibleTo(Row("Agent", AgentActionAuditLog.AgentActor, "jane", "contoso"), admin));
        Assert.True(AgentActionAuditLog.IsVisibleTo(Row("Agent", AgentActionAuditLog.AgentActor, "jane", null), admin));
    }

    [Fact]
    public void Seed_SkipsDeletedCustomersButKeepsUnscopedRows()
    {
        var seeds = AgentActionAuditLog.SeedEntriesFor(
            new HashSet<string>(["betabit", "fenetre"], StringComparer.OrdinalIgnoreCase)).ToArray();

        Assert.NotEmpty(seeds);
        Assert.All(seeds, seed => Assert.True(
            seed.Entry.CustomerId is null or "betabit" or "fenetre"));
        Assert.Contains(seeds, seed => seed.Entry.CustomerId is null);
        Assert.DoesNotContain(seeds, seed => seed.Entry.CustomerId == "contoso");
    }

    [Fact]
    public void Seed_CoversHumanSystemAndAgentRowsInBothModes()
    {
        var entries = AgentActionAuditLog.SeedEntries.Select(seed => seed.Entry).ToArray();

        Assert.True(entries.Length >= 20);
        Assert.Contains(entries, entry => entry.ActorType == "Human");
        Assert.Contains(entries, entry => entry.ActorType == "System");
        Assert.Contains(entries, entry => entry.ActorType == "Agent" && entry.Mode == "Before");
        Assert.Contains(entries, entry => entry.ActorType == "Agent" && entry.Mode == "After");
        Assert.Contains(entries, entry => entry.Outcome == "denied");
        Assert.All(entries, entry => Assert.StartsWith("seed-", entry.InteractionId));
        Assert.Equal(entries.Length, entries.Select(entry => entry.InteractionId).Distinct().Count());
    }

    [Fact]
    public void Detail_IsTruncatedToColumnLength()
    {
        Assert.Equal(500, AgentActionAuditLog.Truncate(new string('x', 900))!.Length);
        Assert.Null(AgentActionAuditLog.Truncate(null));
    }
}
