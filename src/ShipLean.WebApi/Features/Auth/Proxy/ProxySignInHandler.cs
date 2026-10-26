using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Options;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Users;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// The authenticate scheme AuthServices.SessionScheme picks in proxy mode (outside ProxySignInPaths.Skipped). Challenge,
// forbid, sign-in and sign-out stay the cookie's.
//
// A request carrying a proxy identity acts as that person: found or created by email, the name kept in step with the
// provider, and signed in with the ordinary cookie (issued, or replacing one for someone else). A cookie issued for the
// same identity is used as it is, with no database read. A disabled user is refused, and the challenge says so. Without
// an identity the cookie alone decides, as in password mode; nobody is ever created from a cookie.
//
// A sign-in page load that carries an identity is signed in and sent on to its returnUrl (K9). The cookie handler sets
// that redirect when it signs in on its login path, and HandleRequestAsync ends the request there, so the page that
// would otherwise be served does not overwrite it.
public sealed class ProxySignInHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IProxyIdentityReader reader,
    IUserDirectory directory,
    IUserAccounts accounts,
    UserSessions sessions,
    IProxyFirstAdmin firstAdmin,
    CookieSession cookieSession,
    ProxySignInSettings settings,
    IOptionsMonitor<CookieAuthenticationOptions> cookieOptions)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder), IAuthenticationRequestHandler
{
    // Hides the base Scheme property (the AuthenticationScheme instance); this is the name AuthServices registers it under.
    public new const string Scheme = "ProxySignIn";

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var cancellationToken = Context.RequestAborted;
        var identity = await reader.ReadAsync(Request, cancellationToken);
        // Runs CookieSessionEvents.ValidatePrincipal, which signs out a stale or disabled user's cookie.
        var cookie = await Context.AuthenticateAsync(AuthServices.CookieScheme);
        if (identity == null)
        {
            return cookie;
        }
        if (cookie.Succeeded && IssuedFor(cookie.Principal, identity))
        {
            return cookie;
        }

        var user = await directory.FindOrCreateAsync(identity.Email, identity.Name, cancellationToken);
        var snapshot = await sessions.GetFreshAsync(user.Id, cancellationToken)
                       ?? throw new InvalidOperationException($"User {user.Id} was found or created but has no row.");
        if (snapshot.Disabled)
        {
            Logger.LogInformation("Proxy sign-in refused for user {UserId}: the account is disabled", user.Id);
            Context.Items[CookieSessionEvents.DisabledItem] = true;
            // Someone else's still-valid cookie must not outlive the refusal: the next request without an identity
            // would act as them.
            if (cookie.Succeeded)
            {
                await cookieSession.SignOutAsync();
            }
            return AuthenticateResult.NoResult();
        }

        await firstAdmin.PromoteIfFirstAsync(user.Id, identity.Email, cancellationToken);
        await accounts.RecordSignInAsync(user.Id, cancellationToken);
        // The cookie handler keeps the result it authenticated above for the rest of the request, so this request acts
        // as the principal just issued rather than re-reading it.
        var principal = await cookieSession.SignInAsync(user.Id, snapshot.SecurityStamp, identity);
        return AuthenticateResult.Success(new AuthenticationTicket(principal, base.Scheme.Name));
    }

    // Runs on every request, in password mode too, before the default scheme authenticates.
    public async Task<bool> HandleRequestAsync()
    {
        if (!settings.IsProxy || Request.Path != cookieOptions.Get(AuthServices.CookieScheme).LoginPath)
        {
            return false;
        }
        var result = await Context.AuthenticateAsync(Scheme);
        return result.Succeeded && Response.StatusCode == StatusCodes.Status302Found;
    }

    // The cookie was issued by proxy sign-in for this email and, when the provider sends one, this name.
    private static bool IssuedFor(ClaimsPrincipal? principal, ProxyIdentity identity)
        => string.Equals(principal?.FindFirstValue(AuthPrincipal.ProxyEmailClaim), identity.Email, StringComparison.OrdinalIgnoreCase)
           && (identity.Name == null || string.Equals(principal?.FindFirstValue(AuthPrincipal.ProxyNameClaim), identity.Name, StringComparison.Ordinal));
}
