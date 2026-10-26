using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.Filters;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Auth;

// A user who signed in with a one-time password has a session but may do nothing except change the password (or sign
// out) until they have. Applies to every controller action; anonymous actions and [AllowPasswordChange] ones are exempt.
// Not applied in proxy mode: a user left with the flag has no password to change and must not be locked out.
public sealed class PasswordChangeFilter(UserSessions sessions, ProxySignInSettings proxy) : IAsyncAuthorizationFilter
{
    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (proxy.IsProxy)
        {
            return;
        }
        var metadata = context.ActionDescriptor.EndpointMetadata;
        if (metadata.OfType<IAllowAnonymous>().Any() || metadata.OfType<AllowPasswordChangeAttribute>().Any())
        {
            return;
        }
        if (Guid.TryParse(context.HttpContext.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && await sessions.GetAsync(userId, context.HttpContext.RequestAborted) is { MustChangePassword: true })
        {
            throw ApiException.PasswordChangeRequired();
        }
    }
}
