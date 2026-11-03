namespace ShipLean.Contracts.Retros;

// The retros of a project grouped by Feature (GET api/projects/{id}/retros/features).
public interface IRetroFeaturesReader
{
    Task<RetroFeaturesDto> GetAsync(Guid projectId, CancellationToken cancellationToken = default);
}
