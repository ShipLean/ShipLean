using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http.Extensions;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Auth;

// Checks each request's cookie against app_user (through the one-minute UserSessions cache): a disabled user, or a
// cookie issued before the user's security stamp changed (password change, sign out everywhere else, admin reset),
// is signed out. A request to /api that needs a session then gets 401 `session_ended`, never the sign-in page redirect
// a page load gets; an anonymous endpoint (sign-in itself) just sees no user.
public sealed class CookieSessionEvents(UserSessions sessions) : CookieAuthenticationEvents
{
    public const string ReturnUrlParameter = "returnUrl";
    private const string EndedItem = "shiplean.session-ended";

    // Set by proxy sign-in when the proxy's identity belongs to a disabled user: the challenge then answers as for an
    // ended session, and a page load opens the sign-in page with the disabled note.
    public const string DisabledItem = "shiplean.proxy-user-disabled";

    public override async Task ValidatePrincipal(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var valid = Guid.TryParse(principal?.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
                    && principal?.FindFirstValue(AuthPrincipal.StampClaim) is { } stamp
                    && await sessions.GetAsync(userId, context.HttpContext.RequestAborted) is { Disabled: false } user
                    && user.SecurityStamp == stamp;
        if (!valid)
        {
            context.HttpContext.Items[EndedItem] = true;
            context.RejectPrincipal();
            // The hub's negotiate answers 401 session_ended and leaves the cookie, so the API call the SPA makes next
            // answers session_ended too (and deletes the cookie), and the page lands on the sign-in page with the ended note.
            if (!context.Request.Path.StartsWithSegments("/hubs"))
            {
                await context.HttpContext.SignOutAsync(AuthServices.CookieScheme);
            }
        }
    }

    // Written here and not thrown: the error pipeline clears the response, which would drop the Set-Cookie that deletes
    // the ended session's cookie.
    public override Task RedirectToLogin(RedirectContext<CookieAuthenticationOptions> context)
    {
        if (!IsApi(context.Request))
        {
            // A page load refused for a disabled proxy user, or whose session ended, opens the sign-in page with the
            // note, and either way the sign-in page gets the path to come back to.
            if (RefusedState(context.HttpContext.Items) is { } state)
            {
                var returnUrl = Uri.EscapeDataString(context.Request.GetEncodedPathAndQuery());
                context.Response.Redirect($"{context.Options.LoginPath}?state={state}&{ReturnUrlParameter}={returnUrl}");
                return Task.CompletedTask;
            }
            return base.RedirectToLogin(context);
        }
        var error = RefusedState(context.HttpContext.Items) != null ? ApiException.SessionEnded() : ApiException.Unauthorized("Sign in to continue.");
        context.Response.StatusCode = error.Status;
        return context.Response.WriteAsJsonAsync(error.ToApiError(), JsonSerializerOptions.Web, context.HttpContext.RequestAborted);
    }

    // The sign-in page's note for a refused page load, or null for an ordinary sign-in redirect.
    private static string? RefusedState(IDictionary<object, object?> items)
    {
        if (items.ContainsKey(DisabledItem))
        {
            return "disabled";
        }
        return items.ContainsKey(EndedItem) ? "ended" : null;
    }

    public override Task RedirectToAccessDenied(RedirectContext<CookieAuthenticationOptions> context)
    {
        context.Response.StatusCode = StatusCodes.Status403Forbidden;
        return Task.CompletedTask;
    }

    public static bool IsApi(HttpRequest request) => request.Path.StartsWithSegments("/api") || request.Path.StartsWithSegments("/hubs");
}
