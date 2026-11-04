using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Features.Demos.Brand;

public interface IDemoLogoStore
{
    Task<DemoLogoRef?> ReadAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task<BlobContent> OpenAsync(Guid projectId, CancellationToken cancellationToken = default);

    Task RemoveAsync(Guid projectId, CancellationToken cancellationToken = default);
}
