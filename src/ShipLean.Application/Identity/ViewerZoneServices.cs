using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using ShipLean.Contracts;

namespace ShipLean.Application.Identity;

// Singletons, like CurrentUserServices: the zone is read from the request's user at call time. Replace, not Add, so the
// claims transformation wins over the no-op AddAuthentication registers, whichever runs first.
public sealed class ViewerZoneServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<IViewerZone, ViewerZone>();
        services.Replace(ServiceDescriptor.Singleton<IClaimsTransformation, ViewerZoneClaims>());
    }
}
