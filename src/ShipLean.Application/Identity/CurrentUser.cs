using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Identity;

// The signed-in user of the current request: the NameIdentifier claim of the cookie's principal. A call with no such
// user (background work, an anonymous request that reached a service) is a programming error, so it throws rather than
// guess a user.
public sealed class CurrentUser(IServiceProvider services) : ICurrentUser
{
    public Guid Id => FromRequest()
        ?? throw new InvalidOperationException("No signed-in user: this call has no request user.");

    private Guid? FromRequest()
    {
        var user = services.GetService<IHttpContextAccessor>()?.HttpContext?.User;
        return user?.Identity?.IsAuthenticated == true && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : null;
    }
}
