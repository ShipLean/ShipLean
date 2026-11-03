namespace ShipLean.Contracts.Retros;

// Deciding what to do with a point: an action item over one or more points, Not needed with a reason, a theme set by an agent, a merge proposal.
// Callers have run the RetroChecks shape checks. Each method checks the points belong to the project (not_found otherwise).
public interface IRetroDecisionWrites
{
    Task<RetroActionDetailDto> CreateActionAsync(Guid projectId, RetroActionNew request, RetroActor actor, CancellationToken cancellationToken = default);

    Task<RetroActionDetailDto> AddPointsAsync(Guid workItemId, RetroActionAddPoints request, RetroActor actor, CancellationToken cancellationToken = default);

    Task<RetroActionDetailDto> UpdateActionAsync(Guid workItemId, RetroActionPut request, RetroActor actor, CancellationToken cancellationToken = default);

    Task<RetroPointDto> MarkNotNeededAsync(Guid pointId, RetroNotNeededPut request, RetroActor actor, CancellationToken cancellationToken = default);

    Task<RetroPointDto> UndoNotNeededAsync(Guid pointId, RetroActor actor, CancellationToken cancellationToken = default);

    Task<bool> AssignThemeAsync(Guid pointId, string themeName, RetroActor actor, CancellationToken cancellationToken = default);

    Task<bool> ProposeMergeAsync(Guid projectId, string fromTheme, string intoTheme, string reason, RetroActor actor, CancellationToken cancellationToken = default);
}
