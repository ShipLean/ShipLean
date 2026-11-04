using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.Demos.Runs;

// K6's automatic path: a closed sprint starts its demo when the project asked for that. It runs off a request, so it takes the closer as an
// argument and never reads ICurrentUser. It claims the sprint first, so a second instance or a re-delivered event does nothing, and it never throws.
public sealed class SprintDemoAutomaticRun(
    NpgsqlDataSource dataSource,
    IDemoRunStarter starter,
    AgentSessionStarter sessions,
    TimeProvider time,
    IProjectEvents events,
    ILogger<SprintDemoAutomaticRun> logger) : ISprintDemoAutomaticRun
{
    private const string BaseUrlMessage = "The instance has no base URL, so the demo's upload link cannot be made. Set it in the instance settings.";
    public const string FailedMessage = "The demo could not be started. Start it again from the sprint's demo panel.";

    public async Task StartAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken = default)
    {
        try
        {
            await RunAsync(projectId, sprintId, closedBy, cancellationToken);
        }
        catch (Exception failed)
        {
            logger.LogError(failed, "The automatic demo of sprint {SprintId} failed", sprintId);
            await StoreMessageAsync(projectId, sprintId, FailedMessage, CancellationToken.None);
        }
    }

    private async Task RunAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken)
    {
        if (!await IsAutomaticAsync(projectId, cancellationToken))
        {
            return;
        }

        var name = await ClaimAsync(projectId, sprintId, closedBy, cancellationToken);
        if (name is null)
        {
            return;
        }

        await events.PublishAsync(new SprintDemoChanged(projectId, sprintId));

        if (await sessions.PlanProjectAsync(projectId, null, cancellationToken) is { } block)
        {
            await StoreMessageAsync(projectId, sprintId, AgentStartMessages.For(block), cancellationToken);
            return;
        }

        var baseUrl = await BaseUrlAsync(cancellationToken);
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            await StoreMessageAsync(projectId, sprintId, BaseUrlMessage, cancellationToken);
            return;
        }

        var itemId = await starter.GetOrCreateItemAsync(projectId, sprintId, name, cancellationToken);
        var request = new DemoRunRequest(projectId, sprintId, itemId, closedBy, "close", baseUrl.TrimEnd('/'), "ShipLean", null);
        var outcome = await starter.StartSessionAsync(request, cancellationToken);
        if (outcome.Block is { } refused and not AgentStartBlock.LiveSession)
        {
            await StoreMessageAsync(projectId, sprintId, AgentStartMessages.For(refused), cancellationToken);
        }
    }

    private async Task<bool> IsAutomaticAsync(Guid projectId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<bool?>(new CommandDefinition(
            "SELECT automatic FROM project_demo_settings WHERE project_id = @projectId", new { projectId }, cancellationToken: cancellationToken)) ?? false;
    }

    // The sprint's name when this call won the claim, else null. An archived sprint is not claimed.
    private async Task<string?> ClaimAsync(Guid projectId, Guid sprintId, Guid closedBy, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            UPDATE sprint SET demo_requested_by = @closedBy, demo_requested_via = 'close', demo_requested_at = @now
            WHERE id = @sprintId AND project_id = @projectId AND archived_at IS NULL AND demo_requested_at IS NULL
            RETURNING name
            """,
            new { closedBy, now = time.GetUtcNow(), sprintId, projectId }, cancellationToken: cancellationToken));
    }

    private async Task<string?> BaseUrlAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
    }

    private async Task StoreMessageAsync(Guid projectId, Guid sprintId, string message, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE sprint SET demo_blocked_message = @message WHERE id = @sprintId", new { message, sprintId }, cancellationToken: cancellationToken));
        await events.PublishAsync(new SprintDemoChanged(projectId, sprintId));
    }
}
