namespace ShipLean.Contracts.Retros;

// A person's answer to a theme merge proposal. An unknown proposal is not_found; one already decided is a conflict.
public interface IRetroMergeProposals
{
    Task MergeAsync(Guid proposalId, RetroActor actor, CancellationToken cancellationToken = default);

    Task KeepApartAsync(Guid proposalId, RetroActor actor, CancellationToken cancellationToken = default);
}
