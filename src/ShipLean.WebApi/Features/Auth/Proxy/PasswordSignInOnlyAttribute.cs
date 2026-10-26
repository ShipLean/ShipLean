using Microsoft.AspNetCore.Mvc.Filters;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// Marks an action that only means something while ShipLean checks passwords itself (login, password change, one-time
// passwords, an admin changing a user's email). In proxy mode it answers 403 `forbidden` before anything else runs,
// whoever the caller is: there is no break-glass password.
[AttributeUsage(AttributeTargets.Method)]
public sealed class PasswordSignInOnlyAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        if (context.HttpContext.RequestServices.GetRequiredService<ProxySignInSettings>().IsProxy)
        {
            throw ApiException.Forbidden("Sign-in is handled by your identity proxy.");
        }
    }
}
