using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Features.Demos.Brand;

public interface IDemoLogoStore
{
    Task<DemoLogoRef?> ReadAsync(Guid projectId, CancellationToken cancellationToken = default);

    // 404 when the project has no logo.
    Task<BlobContent> OpenAsync(Guid projectId, CancellationToken cancellationToken = default);

    // The same read without the exception: null when no logo is set, its blob is gone, or the stored type is not one a logo can have.
    Task<BlobContent?> TryOpenAsync(Guid projectId, CancellationToken cancellationToken = default);

    // Points the project at a blob already stored and bumps the version.
    Task SetAsync(Guid projectId, string blobKey, Guid userId, CancellationToken cancellationToken = default);

    // Clears the reference and bumps the version; the bytes stay. Does nothing when there is no logo.
    Task RemoveAsync(Guid projectId, CancellationToken cancellationToken = default);
}
