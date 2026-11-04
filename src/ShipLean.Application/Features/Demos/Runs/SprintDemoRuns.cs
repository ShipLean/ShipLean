using Dapper;
using Npgsql;
using ShipLean.Application.Features.Demos.Reads;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Runs;

// K6's button and K10's hand-over. Every decision about who gets the upload link is IDemoRunStarter's; this checks what the caller may do first.
public sealed class SprintDemoRuns(
    NpgsqlDataSource dataSource,
    IDemoRunStarter starter,
    IAgentSessionStarter sessions,
    ISprintDemoReader reader,
    ICurrentUser currentUser) : ISprintDemoRuns
{
    // Closed sprints only. A refusal (not closed, no agent, a live session) happens before anything changes, so a refused retry leaves the current link alive.
    public async Task<SprintDemoDto> CreateFromButtonAsync(Guid sprintId, string requestOrigin, CancellationToken cancellationToken = default)
    {
        var sprint = await ReadSprintAsync(sprintId, cancellationToken);
        if (sprint.RolloverStatus != "decided")
        {
            throw ApiException.Validation("Close the sprint to make its demo.");
        }

        if (await sessions.PlanProjectAsync(sprint.ProjectId, null, cancellationToken) is { } projectBlock)
        {
            throw Refusal(projectBlock);
        }

        if (sprint.DemoWorkItemId is { } existing && (await sessions.PlanAsync(existing, null, null, cancellationToken)).Block is { } itemBlock)
        {
            throw Refusal(itemBlock);
        }

        var itemId = await starter.GetOrCreateItemAsync(sprint.ProjectId, sprintId, sprint.Name, cancellationToken);
        var baseUrl = await BaseUrlAsync(requestOrigin, cancellationToken);
        var request = new DemoRunRequest(sprint.ProjectId, sprintId, itemId, currentUser.Id, "button", baseUrl, "ShipLean", null);
        var outcome = await starter.StartSessionAsync(request, cancellationToken);
        if (outcome.Block is { } block)
        {
            throw Refusal(block);
        }

        return await reader.GetAsync(sprintId, cancellationToken);
    }

    // No agent check and no closed check: the tool always works. The one refusal is a live session on the item, which HandOverAsync throws.
    public async Task<SprintDemoHandover> CreateForAgentAsync(Guid sprintId, string baseUrl, string agentStamp, Guid? authorizationId, CancellationToken cancellationToken = default)
    {
        var sprint = await ReadSprintAsync(sprintId, cancellationToken);
        var itemId = await starter.GetOrCreateItemAsync(sprint.ProjectId, sprintId, sprint.Name, cancellationToken);
        var request = new DemoRunRequest(sprint.ProjectId, sprintId, itemId, currentUser.Id, "mcp", baseUrl, agentStamp, authorizationId);
        return await starter.HandOverAsync(request, cancellationToken);
    }

    private static ApiException Refusal(AgentStartBlock block) => block == AgentStartBlock.LiveSession
        ? ApiException.Conflict("An agent session is already making this demo.")
        : ApiException.Validation(AgentStartMessages.For(block));

    // The instance's base URL when it has one, else the request's own origin.
    private async Task<string> BaseUrlAsync(string requestOrigin, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var configured = await connection.ExecuteScalarAsync<string?>(new CommandDefinition("SELECT base_url FROM instance_settings", cancellationToken: cancellationToken));
        return string.IsNullOrWhiteSpace(configured) ? requestOrigin : configured.TrimEnd('/');
    }

    private async Task<SprintFacts> ReadSprintAsync(Guid sprintId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await connection.QuerySingleOrDefaultAsync<SprintFacts>(new CommandDefinition(
            "SELECT project_id AS projectId, name, rollover_status AS rolloverStatus, demo_work_item_id AS demoWorkItemId FROM sprint WHERE id = @sprintId AND archived_at IS NULL",
            new { sprintId }, cancellationToken: cancellationToken)) ?? throw ApiException.NotFound("That sprint was not found.");
    }

    private sealed record SprintFacts(Guid ProjectId, string Name, string? RolloverStatus, Guid? DemoWorkItemId);
}
