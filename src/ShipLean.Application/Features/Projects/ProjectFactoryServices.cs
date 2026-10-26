using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.Projects;

// Scoped: the factory acts as the current user and uses the scoped sprint store.
public sealed class ProjectFactoryServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddScoped<IProjectFactory, ProjectFactory>();
}
