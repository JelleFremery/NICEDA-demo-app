using System.Net.Http.Json;
using NICEDA.ApiService.Agent;
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
    public async Task ExplorerSqlAndCosmosRespectSelectedIdentityInBothModes()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        foreach (var (path, janeCount, adminCount) in new[]
        {
            ("sql/raw/customers", 3, 5),
            ("sql/optimized/customer-health", 3, 5),
            ("cosmos/raw-support", 4, 8),
            ("cosmos/optimized-support", 4, 10),
        })
        {
            foreach (var user in new[] { "jane", "admin" })
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/data-explorer/{path}");
                request.Headers.Add(AgentMcpContract.UserHeader, user);
                using var response = await fixture.ApiClient.SendAsync(request, deadline.Token);
                response.EnsureSuccessStatusCode();
                var rows = await response.Content.ReadFromJsonAsync<List<System.Text.Json.Nodes.JsonObject>>(deadline.Token);
                Assert.NotNull(rows);
                Assert.Equal(user == "jane" ? janeCount : adminCount, rows.Count);
                if (user == "jane")
                {
                    var isRawSql = path == "sql/raw/customers";
                    Assert.All(rows, row => Assert.True(
                        NICEDA.ApiService.Security.DemoIdentity.Resolve(user).CanAccess(
                            row[isRawSql ? "CustomerId" : "customerId"]!.GetValue<string>(),
                            row[isRawSql ? "Region" : "region"]?.GetValue<string>())));
                }
            }
        }
    }

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
    public async Task CreateFollowUpTask_IdempotencyDeduplicatesAndRejectsConflicts()
    {
        var key = $"integration-{Guid.NewGuid():N}";
        var dueDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(14));
        await using var connection = new SqlConnection(await fixture.GetSqlConnectionStringAsync());
        await connection.OpenAsync();
        var createdIds = new HashSet<int>();
        try
        {
            async Task<(System.Net.HttpStatusCode StatusCode, int? TaskId)> SendAsync(
                string userId,
                string requestKey,
                string reason)
            {
                using var request = new HttpRequestMessage(
                    HttpMethod.Post,
                    AgentMcpContract.CreateFollowUpTaskPath)
                {
                    Content = JsonContent.Create(new { customerId = "techorama", reason, dueDate }),
                };
                request.Headers.Add(AgentMcpContract.UserHeader, userId);
                request.Headers.Add(AgentMcpContract.ModeHeader, "After");
                request.Headers.Add(AgentMcpContract.IdempotencyKeyHeader, requestKey);
                using var response = await fixture.ApiClient.SendAsync(request);
                var body = response.IsSuccessStatusCode
                    ? await response.Content.ReadFromJsonAsync<System.Text.Json.Nodes.JsonObject>()
                    : null;
                return (response.StatusCode, body?["followUpTaskId"]?.GetValue<int>());
            }

            var created = await SendAsync("jane", key, "Idempotency integration task");
            createdIds.Add(created.TaskId!.Value);
            Assert.Equal(System.Net.HttpStatusCode.Created, created.StatusCode);

            var retry = await SendAsync("jane", key, "Idempotency integration task");
            Assert.Equal(System.Net.HttpStatusCode.OK, retry.StatusCode);
            Assert.Equal(created.TaskId, retry.TaskId);

            var conflict = await SendAsync("jane", key, "A conflicting payload");
            Assert.Equal(System.Net.HttpStatusCode.Conflict, conflict.StatusCode);

            var independent = await SendAsync("jane", $"integration-{Guid.NewGuid():N}", "Idempotency integration task");
            createdIds.Add(independent.TaskId!.Value);
            Assert.Equal(System.Net.HttpStatusCode.Created, independent.StatusCode);
            Assert.NotEqual(created.TaskId, independent.TaskId);

            var otherIdentity = await SendAsync("admin", key, "Idempotency integration task");
            createdIds.Add(otherIdentity.TaskId!.Value);
            Assert.Equal(System.Net.HttpStatusCode.Created, otherIdentity.StatusCode);
        }
        finally
        {
            foreach (var id in createdIds)
            {
                await using var cleanup = new SqlCommand(
                    "DELETE FROM optimized.FollowUpTask WHERE FollowUpTaskId=@id;", connection);
                cleanup.Parameters.AddWithValue("@id", id);
                await cleanup.ExecuteNonQueryAsync();
            }
        }
    }

    [DockerFact]
    public async Task CreateFollowUpTask_RejectsCustomerWithDeletionTombstone()
    {
        var customerId = $"idempotency-delete-{Guid.NewGuid():N}"[..48];
        await using var connection = new SqlConnection(await fixture.GetSqlConnectionStringAsync());
        await connection.OpenAsync();
        try
        {
            await using (var seed = new SqlCommand(
                """
                INSERT INTO raw.Customer (CustomerId, CustomerName, Region, SalesRepEmail)
                VALUES (@id, 'Idempotency deletion test', 'EU', 'tests@niceda.example');
                INSERT INTO raw.DeletedCustomer (CustomerId, DeletedAtUtc)
                VALUES (@id, SYSUTCDATETIME());
                """,
                connection))
            {
                seed.Parameters.AddWithValue("@id", customerId);
                await seed.ExecuteNonQueryAsync();
            }

            var command = new CreateFollowUpTaskCommand(
                customerId,
                "Must not be created while deletion is pending",
                DateOnly.FromDateTime(DateTime.UtcNow.AddDays(1)),
                "idempotency-tests",
                $"integration-{Guid.NewGuid():N}");
            await Assert.ThrowsAsync<ArgumentException>(() =>
                FollowUpTaskCommands.CreateFollowUpTaskWithResultAsync(connection, command));
        }
        finally
        {
            await using var cleanup = new SqlCommand(
                """
                DELETE FROM optimized.FollowUpTask WHERE CustomerId=@id;
                DELETE FROM raw.DeletedCustomer WHERE CustomerId=@id;
                DELETE FROM raw.Customer WHERE CustomerId=@id;
                """,
                connection);
            cleanup.Parameters.AddWithValue("@id", customerId);
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
