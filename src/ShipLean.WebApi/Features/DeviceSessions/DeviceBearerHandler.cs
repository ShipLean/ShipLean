using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Features.DeviceSessions;

// The phone app's bearer: `Authorization: Bearer shl_dev_…`, sent here by the Session selector (AuthServices) on /api/*
// in password mode. A live session gives the device principal and is touched (sliding 90 days). A revoked or expired
// one is marked ended, so the challenge, forwarded to the cookie scheme, answers 401 session_ended.
// TODO: hardened next: the user disabled or their security stamp moved on (ended), one-time password (403
// password_change_required), /hubs/* with access_token, and the paths a bearer is never accepted on.
public sealed class DeviceBearerHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, IDeviceSessions sessions, TimeProvider time)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string Scheme = "DeviceBearer";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (DeviceBearerRequest.Secret(Request.Headers.Authorization) is not { } secret)
        {
            return AuthenticateResult.NoResult();
        }
        var session = await sessions.FindBySecretAsync(secret, Context.RequestAborted);
        if (session is null)
        {
            return AuthenticateResult.Fail("Unknown device session.");
        }
        var now = time.GetUtcNow();
        if (!session.IsLiveAt(now))
        {
            CookieSessionEvents.MarkEnded(Context.Items);
            return AuthenticateResult.Fail("The device session ended.");
        }
        await sessions.TouchAsync(session.Id, now, Context.RequestAborted);
        var principal = AuthPrincipal.ForDevice(session.UserId, session.SecurityStamp, session.Id);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme));
    }
}
