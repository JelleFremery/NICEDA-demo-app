using System.ComponentModel;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using ModelContextProtocol.Server;

namespace NICEDA.McpServer.Tools;

[McpServerToolType]
public sealed class CreateFollowUpTaskTool(
    IHttpContextAccessor httpContextAccessor,
    IHttpClientFactory httpClientFactory,
    IConfiguration configuration)
{
    private const string UserHeader = "X-Niceda-User";
    private const string ModeHeader = "X-Niceda-Mode";
    private const string IdempotencyKeyHeader = "X-Niceda-Idempotency-Key";
    private const string CreateFollowUpTaskPath = "/api/data-explorer/sql/optimized/follow-up-tasks";

    [McpServerTool(Name = "create_follow_up_task")]
    [Description("Create a governed follow-up task for an authorized customer in the After dataset.")]
    public async Task<string> CreateFollowUpTask(
        [Description("The customer account identifier.")] string customerId,
        [Description("A concise business reason for the follow-up.")] string reason,
        [Description("The due date in ISO-8601 date format.")] DateOnly dueDate,
        CancellationToken cancellationToken)
    {
        var context = httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("MCP request context is unavailable.");
        // The API service injects these headers outside the model-visible tool schema.
        // A deployed system must authenticate this service boundary rather than trust demo headers.
        var userId = context.Request.Headers[UserHeader].ToString();
        var mode = context.Request.Headers[ModeHeader].ToString();
        var idempotencyKey = context.Request.Headers[IdempotencyKeyHeader].ToString();
        if (string.IsNullOrWhiteSpace(userId) || !IsKnownMode(mode) ||
            string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 100)
        {
            throw new InvalidOperationException("Trusted NICEDA user, dataset, and request idempotency context are required.");
        }

        var allowedCustomerIds = userId.Equals("admin", StringComparison.OrdinalIgnoreCase)
            || userId.Equals("administrator", StringComparison.OrdinalIgnoreCase)
            ? null
            : userId.Equals("jane", StringComparison.OrdinalIgnoreCase)
                || userId.Equals("jane.okafor@niceda.example", StringComparison.OrdinalIgnoreCase)
                ? new HashSet<string>(["betabit", "fenetre", "techorama"], StringComparer.OrdinalIgnoreCase)
                : throw new UnauthorizedAccessException("The selected demo user is not recognized.");
        if (!mode.Equals("After", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Follow-up tasks can only be created in After mode.");
        }

        if (allowedCustomerIds is not null && !allowedCustomerIds.Contains(customerId))
        {
            throw new UnauthorizedAccessException("The selected identity is not authorized for that customer.");
        }

        if (string.IsNullOrWhiteSpace(customerId))
        {
            throw new ArgumentException("CustomerId is required.", nameof(customerId));
        }

        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
        {
            throw new ArgumentException("Reason is required and must not exceed 500 characters.", nameof(reason));
        }

        if (!Uri.TryCreate(configuration["ApiService:BaseUrl"], UriKind.Absolute, out var apiBaseAddress))
        {
            throw new InvalidOperationException("ApiService:BaseUrl must be configured with the ApiService URL.");
        }

        var client = httpClientFactory.CreateClient("ApiService");
        client.BaseAddress = apiBaseAddress;
        using var apiRequest = new HttpRequestMessage(
            HttpMethod.Post,
            CreateFollowUpTaskPath)
        {
            Content = JsonContent.Create(new
            {
                customerId,
                reason,
                dueDate,
            }),
        };
        apiRequest.Headers.Add(UserHeader, userId);
        apiRequest.Headers.Add(ModeHeader, mode);
        apiRequest.Headers.Add(IdempotencyKeyHeader, idempotencyKey);
        using var response = await client.SendAsync(
            apiRequest,
            cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var detail = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new HttpRequestException($"Follow-up task API returned {(int)response.StatusCode}: {detail}");
        }
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var body = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var taskId = body.RootElement.GetProperty("followUpTaskId").GetInt32();
        var outcome = body.RootElement.GetProperty("outcome").GetString();
        return JsonSerializer.Serialize(new
        {
            followUpTaskId = taskId,
            outcome,
            customerId,
            dueDate,
        });
    }

    private static bool IsKnownMode(string mode) =>
        mode.Equals("Before", StringComparison.OrdinalIgnoreCase) ||
        mode.Equals("After", StringComparison.OrdinalIgnoreCase);
}
