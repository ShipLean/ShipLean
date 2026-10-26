using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Identity;

// Hosted services start in registration order and modules register in name order, so this one starts after the
// database module's migration.
public sealed class AdminBootstrapServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddHostedService<AdminBootstrap>();
}
