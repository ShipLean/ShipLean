namespace ShipLean.Contracts.Retros;

// The merge proposals agents made, and a person's answer to one. An unknown proposal is not_found; one already decided is a conflict.
public interface IRetroMergeProposals
{
    // The undecided proposals of a project, oldest first, whose two themes are both still live.
    Task<IReadOnlyList<RetroMergeProposalDto>> PendingAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task MergeAsync(Guid proposalId, RetroActor actor, CancellationToken cancellationToken = default);

    Task KeepApartAsync(Guid proposalId, RetroActor actor, CancellationToken cancellationToken = default);
}
