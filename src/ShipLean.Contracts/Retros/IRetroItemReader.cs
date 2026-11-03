namespace ShipLean.Contracts.Retros;

// One Feature, story or bug with its retros (GET api/projects/{id}/retros/items/{key}). An unknown key is not_found.
public interface IRetroItemReader
{
    Task<RetroItemDto> GetAsync(Guid projectId, string itemKey, CancellationToken cancellationToken = default);
}
