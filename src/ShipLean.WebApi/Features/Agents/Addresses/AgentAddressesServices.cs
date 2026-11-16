using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Features.Agents.Addresses;

public sealed class AgentAddressesServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IAgentAddresses, AgentAddressesReader>();
}
