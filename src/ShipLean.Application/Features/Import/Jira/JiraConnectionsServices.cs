using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Import;

namespace ShipLean.Application.Features.Import.Jira;

public sealed class JiraConnectionsServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IJiraConnections, JiraConnections>();
}
