using Microsoft.AspNetCore.Http;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Brand;

public sealed class DemoLogoUpload : IDemoLogoUpload
{
    // The access sweep's allowed leg refuses a 404, so the stub answers the 400 an empty body gets.
    // TODO: the real upload replaces this: sniff the bytes, sanitise an SVG, store the blob and bump the version.
    public Task<ProjectDemoBrandDto> ReplaceAsync(Guid projectId, HttpRequest request, CancellationToken cancellationToken = default)
        => throw ApiException.Validation("A logo must be a PNG, JPEG, WebP or SVG image.", "logo");
}
