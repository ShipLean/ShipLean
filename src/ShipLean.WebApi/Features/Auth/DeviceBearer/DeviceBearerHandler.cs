using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShipLean.Application.Identity;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Auth.DeviceBearer;

// The phone app's bearer, sent here by the Session selector (AuthServices) in password mode. The record comes from
// IDeviceSessions whatever its state; this handler decides. A live session gives the device principal and is touched
// (sliding 90 days). A revoked one, or one that has expired, or whose user is disabled or whose security stamp moved on
// since the sign-in, is ended and marked ended, so the challenge, forwarded to the cookie scheme, answers 401
// session_ended. The stamp and the disabled flag come from UserSessions (at most a minute old, as for a cookie), and are
// read again fresh before a session is ended over them. A user
// on a one-time password is let through: PasswordChangeFilter answers 403 password_change_required for them.
public sealed class DeviceBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder,
    IDeviceSessions sessions, UserSessions users, IUsageEvents events, IUsageScope scope, TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public new const string Scheme = "DeviceBearer";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (DeviceBearerRequest.Secret(Request) is not { } secret)
        {
            return AuthenticateResult.NoResult();
        }
        var started = time.GetTimestamp();
        var ct = Context.RequestAborted;
        var session = await sessions.FindBySecretAsync(secret, ct);
        if (session is null)
        {
            return AuthenticateResult.Fail("Unknown device session.");
        }
        var now = time.GetUtcNow();
        if (session.RevokedAt != null)
        {
            // Whoever revoked it tracked that; nothing more to record.
            CookieSessionEvents.MarkEnded(Context.Items);
            return AuthenticateResult.Fail("The device session was revoked.");
        }
        if (await EndReasonAsync(session, now, ct) is { } reason)
        {
            // Only the call that ends the session tracks it.
            if (await sessions.EndAsync(session.Id, now, ct))
            {
                SelfEmittedUsage.TrackOk(
                    events, scope, time, started, UsageEventNames.DeviceSessionEnded, session.UserId, "auth:DeviceBearer",
                    properties: new Dictionary<string, object> { [UsageProps.Reason] = reason },
                    surface: UsageSurface.App);
            }
            CookieSessionEvents.MarkEnded(Context.Items);
            return AuthenticateResult.Fail("The device session ended.");
        }
        // The store would skip a write inside the interval anyway; checking here saves the round trip.
        if (session.LastUsedAt <= now - DeviceSessionLimits.TouchInterval)
        {
            await sessions.TouchAsync(session.Id, now, ct);
        }
        var principal = AuthPrincipal.ForDevice(session.UserId, session.SecurityStamp, session.Id);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme));
    }

    // Why a session that is not revoked is over, or null when it still counts.
    private async Task<string?> EndReasonAsync(DeviceSessionRecord session, DateTimeOffset now, CancellationToken ct)
    {
        if (!session.IsLiveAt(now))
        {
            return DeviceSessionEndReasons.Expired;
        }
        // Ending is for good, and the cached user can be up to a minute behind (another instance may have signed this
        // session in under a stamp this one has not seen yet), so a cached answer that would end it is read again fresh.
        if (UserEndReason(session, await users.GetAsync(session.UserId, ct)) is null)
        {
            return null;
        }
        return UserEndReason(session, await users.GetFreshAsync(session.UserId, ct));
    }

    private static string? UserEndReason(DeviceSessionRecord session, UserSnapshot? user)
    {
        if (user is null or { Disabled: true })
        {
            return DeviceSessionEndReasons.UserDisabled;
        }
        return user.SecurityStamp == session.SecurityStamp ? null : DeviceSessionEndReasons.StampChanged;
    }
}
