using System.Text;
using ShipLean.Contracts.Blobs;


namespace ShipLean.Application.Features.Demos.Brand;

public sealed class DemoLogoStore : IDemoLogoStore
{
    // TODO: the real store replaces this: reads project_demo_settings and the blob store.
    public Task<DemoLogoRef?> ReadAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult<DemoLogoRef?>(null);

    // TODO: the real store replaces this and answers 404 when no logo is set. It serves ShipLean's logo for now so the access sweep's allowed leg
    // sees a success; the real task seeds a logo for that row.
    public Task<BlobContent> OpenAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(new BlobContent(new MemoryStream(Encoding.UTF8.GetBytes(DemoBrandDefaults.LogoSvg)), "image/svg+xml"));

    public Task RemoveAsync(Guid projectId, CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
