using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;

namespace ShipLean.Application.Features.AgentDispatch;

public sealed class AgentDispatchServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<AgentSessionStarter>();
        services.AddSingleton<AgentTrigger>();
        services.AddSingleton<AgentSessionEndpoints>();
        services.AddHostedService<AgentDispatchSubscriber>();
    }
}
