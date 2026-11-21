using ShipLean.Application.Features.Agents.Stores;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Prompts;

public sealed class PromptAgentServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton<IAgentClientInfo, AgentClientInfo>();
        services.AddMcpServer().WithMessageFilters(filters => filters.AddIncomingFilter(InitializeClientInfoFilter.Apply));
    }
}
