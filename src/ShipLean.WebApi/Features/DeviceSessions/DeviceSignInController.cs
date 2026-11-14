using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.DeviceSessions;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Features.Auth.Proxy;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.DeviceSessions;

// The phone app's sign-in handoff.
// POST /api/auth/device-sessions/codes    CreateSignInCodeRequest -> SignInCodeDto (the confirm page; cookie session only)
// POST /api/auth/device-sessions/exchange ExchangeSignInCodeRequest -> 201 DeviceSignInDto (anonymous, rate limited)
[Route("api/auth/device-sessions")]
public sealed class DeviceSignInController(
    IDeviceSessions sessions, ProxySignInSettings proxy, MeReader me, ICurrentUser currentUser, IUsageEvents events, IUsageScope scope, TimeProvider time)
    : ApiControllerBase
{
    public const string ProxyRefusal = "The ShipLean app can't sign in to this server yet.";
    public const string Expired = "Sign-in expired. Try again.";

    // The code carries the caller's stamp as it is now (the cookie's, which CookieSessionEvents checked against app_user).
    [HttpPost("codes")]
    [ProducesResponseType<SignInCodeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public async Task<SignInCodeDto> Codes(CreateSignInCodeRequest request, CancellationToken ct)
    {
        if (User.HasClaim(c => c.Type == AuthPrincipal.DeviceSessionClaim))
        {
            throw ApiException.Forbidden("Confirm a new sign-in from the browser.");
        }
        if (proxy.IsProxy)
        {
            throw ApiException.Validation(ProxyRefusal);
        }
        if (!DeviceSecret.IsChallenge(request.Challenge))
        {
            throw ApiException.Validation("The challenge must be 43 base64url characters.", "challenge");
        }
        var name = request.DeviceName?.Trim() ?? string.Empty;
        if (name.Length is 0 or > DeviceSessionLimits.NameMaxLength)
        {
            throw ApiException.Validation($"The device name must be 1 to {DeviceSessionLimits.NameMaxLength} characters.", "deviceName");
        }
        var stamp = User.FindFirstValue(AuthPrincipal.StampClaim) ?? throw ApiException.SessionEnded();
        var code = await sessions.CreateCodeAsync(currentUser.Id, stamp, request.Challenge!, name, time.GetUtcNow(), ct);
        return new SignInCodeDto(code);
    }

    [AllowAnonymous]
    [PasswordSignInOnly]
    [EnableRateLimiting(AuthServices.LoginRateLimit)]
    [HttpPost("exchange")]
    [ProducesResponseType<DeviceSignInDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public async Task<ActionResult<DeviceSignInDto>> Exchange(ExchangeSignInCodeRequest request, CancellationToken ct)
    {
        var started = time.GetTimestamp();
        var signIn = await sessions.ExchangeAsync(request.Code ?? string.Empty, request.Verifier ?? string.Empty, time.GetUtcNow(), ct)
                     ?? throw ApiException.Validation(Expired);
        var session = signIn.Session;
        SelfEmittedUsage.TrackOk(events, scope, time, started, UsageEventNames.DeviceSessionCreated, session.UserId, "api:DeviceSignIn.Exchange",
            surface: UsageSurface.App);
        var dto = new DeviceSignInDto(DeviceSessionDtos.From(session, current: false, time), signIn.Secret, await me.GetAsync(session.UserId, ct));
        // The response holds the secret: no cache keeps it (RFC 6749 §5.1).
        Response.Headers.CacheControl = "no-store";
        return StatusCode(StatusCodes.Status201Created, dto);
    }
}
