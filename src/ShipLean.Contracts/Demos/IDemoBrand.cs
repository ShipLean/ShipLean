using ShipLean.Contracts.Blobs;

namespace ShipLean.Contracts.Demos;

public interface IDemoBrand
{
    Task<ProjectDemoBrandDto> ReadAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<BlobContent> OpenLogoAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<ProjectDemoBrandDto> SaveTokensAsync(Guid projectId, string? tokens, CancellationToken cancellationToken = default);

    Task<ProjectDemoBrandDto> RemoveLogoAsync(Guid projectId, CancellationToken cancellationToken = default);
}
