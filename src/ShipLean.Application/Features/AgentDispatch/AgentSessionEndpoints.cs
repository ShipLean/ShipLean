using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.AgentDispatch;

// The item-level agent routes. Every one needs Member on the item's project, checked before anything else.
public sealed class AgentSessionEndpoints(IProjectAccess access, ICurrentUser currentUser, AgentSessionStarter starter)
{
    public async Task<AgentStartAvailabilityDto> AvailabilityAsync(Guid workItemId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        var plan = await starter.PlanAsync(workItemId, cancellationToken);
        return plan.Block is { } block ? new AgentStartAvailabilityDto(false, block, AgentStartMessages.For(block)) : new AgentStartAvailabilityDto(true, null, string.Empty);
    }

    // A live session is a Conflict (409); no connection or no repository is a Validation error naming what is missing.
    public async Task<AgentSessionStartedDto> StartAsync(Guid workItemId, string requestOrigin, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.WorkItem, workItemId, ProjectRole.Member, cancellationToken);
        var outcome = await starter.StartAsync(workItemId, currentUser.Id, requestOrigin, cancellationToken);
        return outcome switch
        {
            { SessionId: { } id } => new AgentSessionStartedDto(id, AgentSessionStates.Submitted),
            { Block: AgentStartBlock.LiveSession } => throw ApiException.Conflict(AgentStartMessages.For(AgentStartBlock.LiveSession)),
            { Block: { } block } => throw ApiException.Validation(AgentStartMessages.For(block)),
            _ => throw ApiException.Internal(),
        };
    }
}
