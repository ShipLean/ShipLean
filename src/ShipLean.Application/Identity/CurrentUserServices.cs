using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Identity;

// Singletons: the Development host validates scopes, and singleton services inject ICurrentUser. The user is read from
// the request's HttpContext at call time, so a singleton still sees the right user.
public sealed class CurrentUserServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentUser, CurrentUser>();
    }
}
