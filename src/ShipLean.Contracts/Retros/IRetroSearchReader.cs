namespace ShipLean.Contracts.Retros;

// Retrospectives > Search.
public interface IRetroSearchReader
{
    Task<RetroSearchDto> SearchAsync(Guid projectId, RetroSearchQuery query, CancellationToken cancellationToken = default);
}
