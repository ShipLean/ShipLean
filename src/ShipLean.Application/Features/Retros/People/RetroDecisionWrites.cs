using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// Only delegates. A0W fills RetroActionWriter's three methods, A0X fills RetroDecisionWriter and RetroMergeProposalWriter; nobody edits this file.
internal sealed class RetroDecisionWrites(RetroActionWriter actions, RetroDecisionWriter decisions, RetroMergeProposalWriter proposals) : IRetroDecisionWrites
{
    public Task<RetroActionDetailDto> CreateActionAsync(Guid projectId, RetroActionNew request, RetroActor actor, CancellationToken cancellationToken = default)
        => actions.CreateActionAsync(projectId, request, actor, cancellationToken);

    public Task<RetroActionDetailDto> AddPointsAsync(Guid workItemId, RetroActionAddPoints request, RetroActor actor, CancellationToken cancellationToken = default)
        => actions.AddPointsAsync(workItemId, request, actor, cancellationToken);

    public Task<RetroActionDetailDto> UpdateActionAsync(Guid workItemId, RetroActionPut request, RetroActor actor, CancellationToken cancellationToken = default)
        => actions.UpdateActionAsync(workItemId, request, actor, cancellationToken);

    public Task<RetroPointDto> MarkNotNeededAsync(Guid pointId, RetroNotNeededPut request, RetroActor actor, CancellationToken cancellationToken = default)
        => decisions.MarkNotNeededAsync(pointId, request, actor, cancellationToken);

    public Task<RetroPointDto> UndoNotNeededAsync(Guid pointId, RetroActor actor, CancellationToken cancellationToken = default)
        => decisions.UndoNotNeededAsync(pointId, actor, cancellationToken);

    public Task<bool> AssignThemeAsync(Guid pointId, string themeName, RetroActor actor, CancellationToken cancellationToken = default)
        => decisions.AssignThemeAsync(pointId, themeName, actor, cancellationToken);

    public Task<bool> ProposeMergeAsync(Guid projectId, string fromTheme, string intoTheme, string reason, RetroActor actor, CancellationToken cancellationToken = default)
        => proposals.ProposeAsync(projectId, fromTheme, intoTheme, reason, actor, cancellationToken);
}
