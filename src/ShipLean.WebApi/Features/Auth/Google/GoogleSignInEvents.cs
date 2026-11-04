using Dapper;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Auth.Google;

// The handler has checked state, nonce, signature, issuer, audience and expiry. Here Google's verified email becomes a
// user and the ordinary session cookie; Google's own ticket is never signed in. Every failure lands back on the sign-in
// page with a state it explains. Logs name the kind of failure and never a token or an address.
public sealed class GoogleSignInEvents(
    GoogleAccounts accounts,
    CookieSession cookie,
    NpgsqlDataSource dataSource,
    IOptions<ForwardedHeadersOptions> forwarding,
    IUsageEvents events,
    TimeProvider time,
    ILogger<GoogleSignInEvents> logger) : OpenIdConnectEvents
{
    public const string RefusedState = "google-refused";
    public const string FailedState = "google-failed";

    private const string SubjectClaim = "sub";
    private const string EmailClaim = "email";
    private const string EmailVerifiedClaim = "email_verified";
    private const string NameClaim = "name";

    // The address Google sends the browser back to must be the one registered with it, and behind a gateway the request's
    // own host is not that: X-Forwarded-Host is deliberately not applied to requests. So the rule agents' URLs follow:
    // base_url's origin, or the origin the request came in on.
    public override async Task RedirectToIdentityProvider(RedirectContext context)
    {
        await using var connection = await dataSource.OpenConnectionAsync(context.HttpContext.RequestAborted);
        var configured = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT base_url FROM instance_settings", cancellationToken: context.HttpContext.RequestAborted));
        var origin = AgentUrls.Issuer(configured, RequestOrigin.Of(context.Request, forwarding.Value));
        context.ProtocolMessage.RedirectUri = origin + GoogleSignInServices.CallbackPath;
    }

    public override async Task TicketReceived(TicketReceivedContext context)
    {
        context.HandleResponse();
        var started = time.GetTimestamp();
        var cancellationToken = context.HttpContext.RequestAborted;
        var returnUrl = context.ReturnUri ?? "/";
        var claims = context.Principal!;
        var subject = claims.FindFirst(SubjectClaim)?.Value;
        var email = IdentityInput.Email(claims.FindFirst(EmailClaim)?.Value);
        // Stricter than proxy sign-in, which allows a provider that never sends it: Google always does with this scope.
        var verified = string.Equals(claims.FindFirst(EmailVerifiedClaim)?.Value, "true", StringComparison.OrdinalIgnoreCase);
        if (string.IsNullOrEmpty(subject) || email is null || !verified)
        {
            logger.LogWarning("Google sign-in refused: the token has no subject, or no usable verified email");
            context.Response.Redirect(LoginPath(RefusedState, returnUrl));
            return;
        }
        var userId = await accounts.ResolveAsync(subject, email, IdentityInput.Name(claims.FindFirst(NameClaim)?.Value), cancellationToken);
        if (userId is not { } id)
        {
            context.Response.Redirect(LoginPath(RefusedState, returnUrl));
            return;
        }
        await cookie.SignInAsync(id, cancellationToken);
        SelfEmittedUsage.TrackOk(events, time, started, UsageEventNames.AuthSignedIn, id, "system:GoogleSignInEvents");
        context.Response.Redirect(returnUrl);
    }

    // A cancelled consent screen, a stale or forged state, or Google unreachable.
    public override Task RemoteFailure(RemoteFailureContext context)
    {
        logger.LogWarning("Google sign-in failed ({ErrorType})", context.Failure?.GetType().Name ?? "unknown");
        context.HandleResponse();
        context.Response.Redirect(LoginPath(FailedState, context.Properties?.RedirectUri ?? "/"));
        return Task.CompletedTask;
    }

    public static string LoginPath(string state, string returnUrl)
        => $"/login?state={state}&returnUrl={Uri.EscapeDataString(returnUrl)}";
}
