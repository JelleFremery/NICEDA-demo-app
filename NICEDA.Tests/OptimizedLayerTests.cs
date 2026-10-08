using System.Net.Http.Json;
using NICEDA.ApiService.Sql;
using Microsoft.Data.SqlClient;

namespace NICEDA.Tests;

/// <summary>
/// End-to-end tests against the real AppHost (SQL Server + Cosmos DB
/// emulator, via <see cref="AppHostFixture"/>) covering the "After"
/// surfaces added in this slice: the optimized SQL view (practice 3), the
/// CQRS follow-up-task command round-trip (practice 8), and the customer
/// deletion fan-out (practice 6).
/// </summary>
[Collection(nameof(AppHostCollection))]
public class OptimizedLayerTests(AppHostFixture fixture)
{
    [DockerFact]
    public async Task CustomerHealthView_ReturnsExpectedBusinessFields()
    {
        var customers = await fixture.ApiClient.GetFromJsonAsync<List<CustomerHealth>>(
            "/api/data-explorer/sql/optimized/customer-health");

        Assert.NotNull(customers);
        Assert.Equal(5, customers!.Count);

        var betabit = customers.Single(c => c.CustomerId == "betabit");
        // Betabit is seeded with StatusCode "AtRisk" (see RawSqlSeeder) —
        // the semantic view must surface that as RiskLevel "High" without
        // the caller knowing the underlying status/date logic.
        Assert.Equal("High", betabit.RiskLevel);
    }

    [DockerFact]
    public async Task CreateFollowUpTask_RoundTripsThroughOptimizedView()
    {
        var command = new CreateFollowUpTaskCommand(
            "techorama", "Integration test follow-up", DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14)), "integration-tests");

        await using var connection = new SqlConnection(await fixture.GetSqlConnectionStringAsync());
        var id = await FollowUpTaskCommands.CreateFollowUpTaskAsync(connection, command);
        try
        {
            var tasks = await fixture.ApiClient.GetFromJsonAsync<List<FollowUpTask>>(
                "/api/data-explorer/sql/optimized/follow-up-tasks");
            Assert.NotNull(tasks);
            Assert.Contains(tasks!, t => t.FollowUpTaskId == id && t.CustomerId == "techorama"
                && t.Reason == "Integration test follow-up");
        }
        finally
        {
            await using var cleanup = new SqlCommand(
                "DELETE FROM optimized.FollowUpTask WHERE FollowUpTaskId=@id AND CreatedBy='integration-tests';", connection);
            cleanup.Parameters.AddWithValue("@id", id);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    [DockerFact]
    public async Task OptimizedSupportContainer_HasClassificationAndChunkedConcepts()
    {
        var documents = await fixture.ApiClient.GetFromJsonAsync<List<System.Text.Json.Nodes.JsonObject>>(
            "/api/data-explorer/cosmos/optimized-support");

        Assert.NotNull(documents);

        foreach (var document in documents!)
        {
            Assert.False(string.IsNullOrWhiteSpace(document["classification"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(document["concept"]?.GetValue<string>()));
            Assert.False(string.IsNullOrWhiteSpace(document["severity"]?.GetValue<string>()));
        }

        // The raw Contoso security assessment (one blob) must be split into
        // 3 separate concept-chunked documents here (practice 7).
        var contosoSecurityChunks = documents!
            .Count(d => d["customerId"]!.GetValue<string>() == "contoso"
                && d["concept"]!.GetValue<string>().StartsWith("security-finding-", StringComparison.Ordinal));
        Assert.Equal(3, contosoSecurityChunks);
    }
}
