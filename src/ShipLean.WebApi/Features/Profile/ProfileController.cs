using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Profile;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Profile;
using ShipLean.Contracts.Users;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Profile;

// The caller's own identity. Each answers the caller's MeDto.
[Route("api/me")]
public sealed class ProfileController(ProfileEndpoints endpoints) : ApiControllerBase
{
    [HttpPut("profile")]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public Task<MeDto> Update(UpdateProfileRequest request, CancellationToken cancellationToken)
        => endpoints.UpdateAsync(request, cancellationToken);

    // The file arrives as multipart form data in the "file" part. The action binds no form.
    [HttpPut("photo")]
    [StreamedUpload]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public Task<MeDto> ReplacePhoto(CancellationToken cancellationToken)
        => endpoints.ReplacePhotoAsync(Request, cancellationToken);

    [HttpDelete("photo")]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    public Task<MeDto> RemovePhoto(CancellationToken cancellationToken)
        => endpoints.RemovePhotoAsync(cancellationToken);
}
