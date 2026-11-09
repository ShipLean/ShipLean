using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Features.Usage;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.DeviceSessions;

// The caller's signed-in phones.
// GET    /api/auth/device-sessions          -> DeviceSessionDto[] (live ones, newest first; current marks this request's)
// DELETE /api/auth/device-sessions/{id}     -> 204 (signs one of the caller's phones out; anyone else's is 404)
// DELETE /api/auth/device-sessions/current  -> 204 (ends the session this request used; 404 from a browser)
[Route("api/auth/device-sessions")]
public sealed class DeviceSessionsController(IDeviceSessions sessions, ICurrentUser currentUser, IUsageEvents events, TimeProvider time)
    : ApiControllerBase
{
    private const string NotFoundMessage = "That device isn't signed in.";

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DeviceSessionDto>>(StatusCodes.Status200OK)]
    public async Task<IReadOnlyList<DeviceSessionDto>> List(CancellationToken ct)
    {
        var current = CurrentSessionId();
        var list = await sessions.ListAsync(currentUser.Id, time.GetUtcNow(), ct);
        return list.Select(s => DeviceSessionDtos.From(s, s.Id == current, time)).ToList();
    }

    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Revoke(Guid id, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        if (!await sessions.RevokeAsync(currentUser.Id, id, time.GetUtcNow(), ct))
        {
            throw ApiException.NotFound(NotFoundMessage);
        }
        TrackEnded(started, DeviceSessionEndReasons.WebSignOut, "api:DeviceSessions.Revoke");
        return NoContent();
    }

    [HttpDelete("current")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<NoContentResult> Current(CancellationToken ct)
    {
        var started = time.GetTimestamp();
        if (CurrentSessionId() is not { } id || !await sessions.EndAsync(id, time.GetUtcNow(), ct))
        {
            throw ApiException.NotFound("This request didn't use a device session.");
        }
        TrackEnded(started, DeviceSessionEndReasons.AppSignOut, "api:DeviceSessions.Current");
        return NoContent();
    }

    private Guid? CurrentSessionId()
        => Guid.TryParse(User.FindFirstValue(AuthPrincipal.DeviceSessionClaim), out var id) ? id : null;

    private void TrackEnded(long started, string reason, string source)
        => SelfEmittedUsage.TrackOk(
            events, time, started, UsageEventNames.DeviceSessionEnded, currentUser.Id, source,
            properties: new Dictionary<string, object> { [UsageProps.Reason] = reason },
            surface: UsageSurfaces.Of(HttpContext));
}
