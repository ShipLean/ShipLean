using Microsoft.AspNetCore.Http;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.Demos.Upload;

public sealed class DemoUploads : IDemoUploads
{
    // TODO: the real upload replaces this: find the token, check membership, read the three parts and hand them to IDemoDelivery.
    public Task<DemoUploadedDto> UploadAsync(string token, HttpRequest request, CancellationToken cancellationToken = default)
        => throw ApiException.NotFound("That upload link is not valid.");
}
