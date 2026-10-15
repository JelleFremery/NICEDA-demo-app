using NICEDA.ApiService.Security;
using NICEDA.ApiService.Sql;
using Microsoft.Data.SqlClient;

namespace NICEDA.ApiService.Agent;

public static class AgentChatEndpoints
{
    public static void MapAgentChatEndpoints(this WebApplication app)
    {
        app.MapPost(AgentMcpContract.ChatPath, async (
            AgentChatRequest request,
            AgentChatService agent,
            HttpContext httpContext,
            CancellationToken cancellationToken) =>
        {
            httpContext.Response.Headers.CacheControl = "no-store";
            return await HandleAsync(request, agent, cancellationToken);
        })
        .WithName("AgentChat");

        app.MapPost(AgentMcpContract.ChatFeedbackPath, async (
            AgentFeedbackRequest request,
            AgentChatService agent,
            CancellationToken cancellationToken) =>
            await HandleFeedbackAsync(request, agent, cancellationToken))
        .WithName("AgentChatFeedback");

        app.MapPost(AgentMcpContract.CreateFollowUpTaskPath, HandleCreateFollowUpTaskAsync)
            .WithName("CreateGovernedFollowUpTask");
    }

    public static async Task<IResult> HandleAsync(
        AgentChatRequest request, AgentChatService agent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Message) || request.Message.Length > 4000)
        {
            return Results.BadRequest(new { error = "Message is required and must not exceed 4000 characters." });
        }

        if (!AgentModes.TryParse(request.Mode, out _) || string.IsNullOrWhiteSpace(request.UserId) ||
            string.IsNullOrWhiteSpace(request.RequestIdempotencyKey) || request.RequestIdempotencyKey.Length > 100)
        {
            return Results.BadRequest(new { error = "A valid Before/After mode, userId, and 1-100 character request idempotency key are required." });
        }

        try
        {
            return Results.Ok(await agent.ChatAsync(request, cancellationToken));
        }
        catch (FoundryNotConfiguredException exception)
        {
            return Results.Problem(
                title: "Agent service is not configured",
                detail: exception.Message,
                statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Results.Problem(
                title: "The selected demo identity is not authorized",
                detail: exception.Message,
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    public static async Task<IResult> HandleFeedbackAsync(
        AgentFeedbackRequest request, AgentChatService agent, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.InteractionId) || string.IsNullOrWhiteSpace(request.UserId))
        {
            return Results.BadRequest(new { error = "interactionId and userId are required." });
        }

        try
        {
            await agent.RecordFeedbackAsync(request, cancellationToken);
            return Results.NoContent();
        }
        catch (KeyNotFoundException exception)
        {
            return Results.NotFound(new { error = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return Results.Problem(
                title: "The selected demo identity is not authorized",
                detail: exception.Message,
                statusCode: StatusCodes.Status403Forbidden);
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }

    public static async Task<IResult> HandleCreateFollowUpTaskAsync(
        CreateFollowUpTaskRequest request,
        HttpContext httpContext,
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        var userId = httpContext.Request.Headers[AgentMcpContract.UserHeader].ToString();
        var mode = httpContext.Request.Headers[AgentMcpContract.ModeHeader].ToString();
        if (string.IsNullOrWhiteSpace(userId) || !AgentModes.TryParse(mode, out var parsedMode))
        {
            return Results.BadRequest(new { error = "Trusted NICEDA user and dataset context headers are required." });
        }

        DemoUser identity;
        try
        {
            identity = DemoIdentity.Resolve(userId);
        }
        catch (UnauthorizedAccessException exception)
        {
            return Results.Problem(
                title: "The selected demo identity is not authorized",
                detail: exception.Message,
                statusCode: StatusCodes.Status403Forbidden);
        }

        if (parsedMode != AgentMode.After)
        {
            return Results.BadRequest(new { error = "Follow-up tasks can only be created in After mode." });
        }

        if (!identity.CanAccess(request.CustomerId, null))
        {
            return Results.Problem(
                title: "The selected demo identity is not authorized for this customer",
                statusCode: StatusCodes.Status403Forbidden);
        }

        var command = new CreateFollowUpTaskCommand(
            request.CustomerId,
            request.Reason,
            request.DueDate,
            identity.UserId,
            httpContext.Request.Headers[AgentMcpContract.IdempotencyKeyHeader].ToString());
        if (string.IsNullOrWhiteSpace(command.IdempotencyKey) || command.IdempotencyKey.Length > 100)
        {
            return Results.BadRequest(new { error = "A trusted 1-100 character idempotency key is required." });
        }
        try
        {
            var creation = await FollowUpTaskCommands.CreateFollowUpTaskWithResultAsync(
                connection,
                command,
                cancellationToken);
            var body = new
            {
                FollowUpTaskId = creation.FollowUpTaskId,
                Outcome = creation.Created ? "created" : "existing",
            };
            return creation.Created
                ? Results.Created($"{AgentMcpContract.CreateFollowUpTaskPath}/{creation.FollowUpTaskId}", body)
                : Results.Ok(body);
        }
        catch (IdempotencyKeyConflictException exception)
        {
            return Results.Conflict(new { error = exception.Message });
        }
        catch (ArgumentException exception)
        {
            return Results.BadRequest(new { error = exception.Message });
        }
    }
}

public sealed class FoundryNotConfiguredException()
    : InvalidOperationException("Foundry:Endpoint and Foundry:ApiKey must be configured to answer agent requests.");
