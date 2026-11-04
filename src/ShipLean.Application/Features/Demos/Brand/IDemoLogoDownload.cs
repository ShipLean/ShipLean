using Microsoft.AspNetCore.Http;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Features.Demos.Brand;

public interface IDemoLogoDownload
{
    Task<BlobContent> OpenAsync(string token, HttpRequest request, CancellationToken cancellationToken = default);
}
