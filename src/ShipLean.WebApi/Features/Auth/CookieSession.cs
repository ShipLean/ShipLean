using System.Security.Claims;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Authentication;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Users;
using ShipLean.WebApi.Features.Auth.Proxy;

namespace ShipLean.WebApi.Features.Auth;

// Issues and clears this request's sign-in cookie.
[RegisterSingleton]
public sealed class CookieSession(IServiceProvider services, IUserAccounts accounts, UserSessions sessions)
{
    // A session for the user under their current stamp.
    public async Task SignInAsync(Guid userId, CancellationToken cancellationToken)
        => await SignInAsync(userId, await accounts.GetSecurityStampAsync(userId, cancellationToken));

    // A session under a stamp the caller has just rotated: this session survives, every other one ends.
    public Task SignInAsync(Guid userId, string stamp) => IssueAsync(userId, AuthPrincipal.For(userId, stamp));

    // A proxy sign-in's session under the user's current stamp, carrying the identity it was issued for. Replaces this
    // request's cookie, whoever it was for. The principal is returned so the request can act as it: the cookie handler
    // keeps its earlier authenticate result for the rest of the request.
    public async Task<ClaimsPrincipal> SignInAsync(Guid userId, string stamp, ProxyIdentity identity)
    {
        var principal = AuthPrincipal.ForProxy(userId, stamp, identity.Email, identity.Name);
        await IssueAsync(userId, principal);
        return principal;
    }

    private Task IssueAsync(Guid userId, ClaimsPrincipal principal)
    {
        sessions.Forget(userId);
        return Http.SignInAsync(AuthServices.CookieScheme, principal, new AuthenticationProperties { IsPersistent = true });
    }

    public Task SignOutAsync() => Http.SignOutAsync(AuthServices.CookieScheme);

    private HttpContext Http => services.GetRequiredService<IHttpContextAccessor>().HttpContext ?? throw new InvalidOperationException("No request to sign in on.");
}
