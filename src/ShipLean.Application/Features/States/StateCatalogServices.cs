using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Projects;

namespace ShipLean.Application.Features.States;

public sealed class StateCatalogServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IStateCatalog, StateCatalog>();
}
