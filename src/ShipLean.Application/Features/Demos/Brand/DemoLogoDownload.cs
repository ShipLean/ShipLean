using Microsoft.AspNetCore.Http;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Brand;

public sealed class DemoLogoDownload : IDemoLogoDownload
{
    // TODO: the real download replaces this: find the logo token, require membership and open the project's current logo.
    public Task<BlobContent> OpenAsync(string token, HttpRequest request, CancellationToken cancellationToken = default)
        => throw ApiException.NotFound("That download link is not valid.");
}
