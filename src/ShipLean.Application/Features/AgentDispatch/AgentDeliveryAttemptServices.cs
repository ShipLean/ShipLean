using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

// A singleton like the other agent stores: it holds no per-request state.
public sealed class AgentDeliveryAttemptServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IAgentDeliveryAttempts, AgentDeliveryAttempts>();
}
