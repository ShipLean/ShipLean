using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.AgentDispatch;

// The item-level agent routes. Every one needs Member on the item's project, checked before anything else.
public sealed class AgentSessionEndpoints(IProjectAccess access, ICurrentUser currentUser, AgentSessionStarter starter, AgentPrompter prompter, AgentPanelReader panel, IAgentConnectionWarnings warnings)
{
    // The whole Agent panel in one response: sessions newest first, and whether Start agent works.
    public async Task<AgentPanelDto> SessionsAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var (_, role) = await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        var availability = await AvailabilityCoreAsync(workItemId, cancellationToken);
        var sessions = await panel.SessionsAsync(workItemId, role, cancellationToken);
        return new AgentPanelDto(sessions.Count > 0 || availability.Reason is not AgentStartBlock.NoConnection, availability, sessions);
    }

    public async Task<AgentStartAvailabilityDto> AvailabilityAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        return await AvailabilityCoreAsync(workItemId, cancellationToken);
    }

    // Warning is the connection's last-delivery warning whenever the project has a connection (C7), whether or not Start agent works.
    private async Task<AgentStartAvailabilityDto> AvailabilityCoreAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        var plan = await starter.PlanAsync(workItemId, null, null, cancellationToken);
        var warning = plan.Block is AgentStartBlock.NoConnection ? null : await warnings.ForConnectionAsync(plan.ConnectionId, cancellationToken);
        return plan.Block is { } block
            ? new AgentStartAvailabilityDto(false, block, AgentStartMessages.For(block), warning)
            : new AgentStartAvailabilityDto(true, null, string.Empty, warning);
    }

    // A live session is a Conflict (409); no connection or no repository is a Validation error naming what is missing.
    public async Task<AgentSessionStartedDto> StartAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        var outcome = await starter.StartAsync(workItemId, currentUser.Id, null, null, null, cancellationToken);
        return outcome switch
        {
            { SessionId: { } id } => new AgentSessionStartedDto(id, AgentSessionStates.Submitted),
            { Block: AgentStartBlock.LiveSession } => throw ApiException.Conflict(AgentStartMessages.For(AgentStartBlock.LiveSession)),
            { Block: { } block } => throw ApiException.Validation(AgentStartMessages.For(block)),
            _ => throw ApiException.Internal(),
        };
    }

    // Stop: sends the agent a `prompted` with signal "stop". The session stays live until the agent answers (or 10 minutes pass).
    // A second Stop on a session already stopping changes nothing. No live session is a Conflict (409).
    public async Task StopAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        if (await prompter.StopAsync(workItemId, currentUser.Id, cancellationToken) == AgentPromptOutcome.NoLiveSession)
        {
            throw ApiException.Conflict("No agent session is running on this item.");
        }
    }
}
