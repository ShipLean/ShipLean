using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Identity;

public sealed class ProjectAccessServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // The concrete ProjectAccess stays behind the decorator, so IProjectAccess has one registration.
        services.AddSingleton<ProjectAccess>();
        services.AddSingleton<IProjectAccess, UsageStampingProjectAccess>();
    }
}
