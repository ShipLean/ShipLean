using System.Security.Claims;
using Agoda.IoC.Core;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Realtime;

// Whether a hub connection's session still counts, with the freshness controllers have (UserSessions, at most a minute
// old). The cookie handler checked all this when the socket opened; a socket outlives it, so every invocation and the
// sweep ask again.
[RegisterSingleton]
public sealed class HubSessionCheck(UserSessions sessions)
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
        return user.MustChangePassword ? ApiException.PasswordChangeRequired() : null;
    }
}
