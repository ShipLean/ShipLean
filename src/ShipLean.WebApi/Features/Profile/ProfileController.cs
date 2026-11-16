using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Profile;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Profile;
using ShipLean.Contracts.Usage;
using ShipLean.Contracts.Users;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Profile;

// The caller's own identity. Each answers the caller's MeDto.
[Route("api/me")]
public sealed class ProfileController(ProfileEndpoints endpoints, IUsageEvents events, IUsageScope scope, TimeProvider time) : ApiControllerBase
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

    // Tracks profile.time_zone_set only when the stored zone changed, with where it came from.
    [HttpPut("time-zone")]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<MeDto> SetTimeZone(SetTimeZoneRequest request, CancellationToken cancellationToken)
    {
        var started = time.GetTimestamp();
        var (me, changed) = await endpoints.SetTimeZoneAsync(request, cancellationToken);
        if (changed)
        {
            SelfEmittedUsage.TrackOk(events, scope, time, started, UsageEventNames.ProfileTimeZoneSet, me.Id, "api:Profile.SetTimeZone",
                properties: new Dictionary<string, object> { [UsageProps.From] = request.Source == TimeZoneSource.Profile ? "profile" : "browser" });
        }
        return me;
    }

    [HttpGet("time-zones")]
    [ProducesResponseType<TimeZoneChoicesDto>(StatusCodes.Status200OK)]
    public TimeZoneChoicesDto TimeZones() => endpoints.TimeZoneChoices();
}
