using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using NICEDA.ApiService.Sql;

namespace NICEDA.Tests;

public sealed class McpDockerFactAttribute : FactAttribute
{
    public McpDockerFactAttribute()
    {
        if (Environment.GetEnvironmentVariable("NICEDA_TEST_MCP_URL") is null
            || Environment.GetEnvironmentVariable("NICEDA_TEST_API_URL") is null)
            Skip = "Opt-in MCP transport check: set NICEDA_TEST_MCP_URL and the live Docker test settings.";
    }
}

[Collection(nameof(AppHostCollection))]
public class GovernedTransportTests(AppHostFixture fixture)
{
    [McpDockerFact]
    public async Task RealMcpCreatesAuthorizedTaskAndRejectsBeforeAndOutOfScope()
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(45));
        using var client = new HttpClient
        {
            BaseAddress = new Uri(Environment.GetEnvironmentVariable("NICEDA_TEST_MCP_URL")!),
            Timeout = TimeSpan.FromSeconds(15),
        };
        client.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        client.DefaultRequestHeaders.Accept.ParseAdd("text/event-stream");
        client.DefaultRequestHeaders.Add("X-Niceda-User", "jane");
        client.DefaultRequestHeaders.Add("X-Niceda-Mode", "After");
        var idempotencyKey = $"mcp-integration-{Guid.NewGuid():N}";
        client.DefaultRequestHeaders.Add("X-Niceda-Idempotency-Key", idempotencyKey);
        var initialized = await SendAsync(client, "initialize", new
        {
            protocolVersion = "2025-06-18",
            capabilities = new { },
            clientInfo = new { name = "niceda-tests", version = "1.0" },
        }, deadline.Token);
        Assert.True(initialized.TryGetProperty("result", out _));
        client.DefaultRequestHeaders.Add("MCP-Protocol-Version", "2025-06-18");

        var reason = $"MCP integration {Guid.NewGuid():N}";
        var due = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(7)).ToString("yyyy-MM-dd");
        await using var sql = new SqlConnection(await fixture.GetSqlConnectionStringAsync());
        await sql.OpenAsync(deadline.Token);
        try
        {
            var created = await SendAsync(client, "tools/call", new
            {
                name = "create_follow_up_task",
                arguments = new { customerId = "techorama", reason, dueDate = due },
            }, deadline.Token);
            Assert.False(IsError(created));
            var createdTaskId = ReadFollowUpTaskId(created);
            var tasks = await fixture.ApiClient.GetFromJsonAsync<List<FollowUpTask>>(
                "/api/data-explorer/sql/optimized/follow-up-tasks", deadline.Token);
            Assert.Single(tasks!, t => t.Reason == reason && t.CreatedBy == "jane");

            var retry = await SendAsync(client, "tools/call", new
            {
                name = "create_follow_up_task",
                arguments = new { customerId = "techorama", reason, dueDate = due },
            }, deadline.Token);
            Assert.False(IsError(retry));
            Assert.Equal(createdTaskId, ReadFollowUpTaskId(retry));
            tasks = await fixture.ApiClient.GetFromJsonAsync<List<FollowUpTask>>(
                "/api/data-explorer/sql/optimized/follow-up-tasks", deadline.Token);
            Assert.Single(tasks!, t => t.Reason == reason && t.CreatedBy == "jane");

            var conflict = await SendAsync(client, "tools/call", new
            {
                name = "create_follow_up_task",
                arguments = new { customerId = "techorama", reason = $"{reason} changed", dueDate = due },
            }, deadline.Token);
            Assert.True(IsError(conflict));
            tasks = await fixture.ApiClient.GetFromJsonAsync<List<FollowUpTask>>(
                "/api/data-explorer/sql/optimized/follow-up-tasks", deadline.Token);
            Assert.Single(tasks!, t => t.Reason == reason && t.CreatedBy == "jane");

            client.DefaultRequestHeaders.Remove("X-Niceda-Mode");
            client.DefaultRequestHeaders.Add("X-Niceda-Mode", "Before");
            Assert.True(IsError(await SendAsync(client, "tools/call", new
            {
                name = "create_follow_up_task",
                arguments = new { customerId = "techorama", reason, dueDate = due },
            }, deadline.Token)));
            client.DefaultRequestHeaders.Remove("X-Niceda-Mode");
            client.DefaultRequestHeaders.Add("X-Niceda-Mode", "After");
            Assert.True(IsError(await SendAsync(client, "tools/call", new
            {
                name = "create_follow_up_task",
                arguments = new { customerId = "contoso", reason, dueDate = due, userId = "admin" },
            }, deadline.Token)));
        }
        finally
        {
            await using var cleanup = new SqlCommand(
                "DELETE FROM optimized.FollowUpTask WHERE IdempotencyKey=@idempotencyKey AND CreatedBy='jane';", sql);
            cleanup.Parameters.AddWithValue("@idempotencyKey", idempotencyKey);
            await cleanup.ExecuteNonQueryAsync();
        }
    }

    private static int ReadFollowUpTaskId(JsonElement message)
    {
        var result = message.GetProperty("result");
        if (result.TryGetProperty("structuredContent", out var structured) &&
            structured.TryGetProperty("followUpTaskId", out var structuredId))
        {
            return structuredId.GetInt32();
        }

        foreach (var item in result.GetProperty("content").EnumerateArray())
        {
            if (item.TryGetProperty("text", out var text))
            {
                using var document = JsonDocument.Parse(text.GetString()!);
                if (document.RootElement.TryGetProperty("followUpTaskId", out var taskId))
                {
                    return taskId.GetInt32();
                }
            }
        }

        throw new Xunit.Sdk.XunitException("The MCP action response did not expose a follow-up task ID.");
    }

    private static bool IsError(JsonElement message) =>
        message.TryGetProperty("error", out _)
        || (message.GetProperty("result").TryGetProperty("isError", out var error) && error.GetBoolean());

    private static async Task<JsonElement> SendAsync(HttpClient client, string method, object parameters, CancellationToken ct)
    {
        using var response = await client.PostAsJsonAsync("", new { jsonrpc = "2.0", id = 1, method, @params = parameters }, ct);
        response.EnsureSuccessStatusCode();
        if (response.Headers.TryGetValues("Mcp-Session-Id", out var session))
        {
            client.DefaultRequestHeaders.Remove("Mcp-Session-Id");
            client.DefaultRequestHeaders.Add("Mcp-Session-Id", session);
        }
        var text = await response.Content.ReadAsStringAsync(ct);
        var json = text.StartsWith("event:", StringComparison.Ordinal)
            ? text.Split('\n').Single(line => line.StartsWith("data: ", StringComparison.Ordinal))[6..]
            : text;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }
}
