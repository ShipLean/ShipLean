using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Profile;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Profile;

// The address a user's photoUrl points at; any signed-in user may read any user's photo.
[Route("api/users/{id:guid}/photo")]
public sealed class UserPhotoController(ProfileEndpoints endpoints) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(Guid id, [FromQuery] string? v, CancellationToken cancellationToken)
        => ResultOf(await endpoints.OpenPhotoAsync(id, v, cancellationToken));

    private static PhotoFileResult ResultOf((BlobContent Content, bool VersionIsCurrent) photo)
        => new(photo.Content.Content, photo.Content.ContentType, photo.VersionIsCurrent);
}
