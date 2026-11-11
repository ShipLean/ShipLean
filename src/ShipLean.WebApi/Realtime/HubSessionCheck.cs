using System.Security.Claims;
using Agoda.IoC.Core;
using ShipLean.Application.Identity;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Realtime;

// Whether a hub connection's session still counts, with the freshness controllers have (UserSessions, at most a minute
// old). The cookie handler (or the device bearer) checked all this when the socket opened; a socket outlives it, so every
// invocation and the sweep ask again. A socket opened with a device bearer also needs its device session still live:
// revoking a lost phone, or the session expiring, ends the socket too.
[RegisterSingleton]
public sealed class HubSessionCheck(UserSessions sessions, IDeviceSessions deviceSessions, TimeProvider time)
{
    // Null when the session is fine; otherwise the refusal. The principal must carry a stamp (every cookie sign-in does)
    // and it must still match the user's current one; an identity with none is refused, never waved through.
    public async Task<ApiException?> RefusalAsync(ClaimsPrincipal? principal, CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            || await sessions.GetAsync(userId, cancellationToken) is not { } user
            || user.Disabled
            || principal.FindFirstValue(AuthPrincipal.StampClaim) != user.SecurityStamp)
        {
            return ApiException.SessionEnded();
        }
        if (principal.FindFirst(AuthPrincipal.DeviceSessionClaim) is { } device && !await DeviceSessionLiveAsync(device.Value, cancellationToken))
        {
            return ApiException.SessionEnded();
        }
        return user.MustChangePassword ? ApiException.PasswordChangeRequired() : null;
    }

    // A device principal whose claim is not a session id, or names no session, is refused like a revoked one.
    private async Task<bool> DeviceSessionLiveAsync(string claim, CancellationToken cancellationToken)
        => Guid.TryParse(claim, out var id)
           && await deviceSessions.FindByIdAsync(id, cancellationToken) is { } session
           && session.IsLiveAt(time.GetUtcNow());
}
