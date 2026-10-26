using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Instance.Integrations;

public sealed class IntegrationSettingsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddScoped<IIntegrationSettings, DbIntegrationSettings>();
}
