using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Test;

public sealed class AgentConnectionTestServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddSingleton<IAgentConnectionTester, AgentConnectionTester>();
}
