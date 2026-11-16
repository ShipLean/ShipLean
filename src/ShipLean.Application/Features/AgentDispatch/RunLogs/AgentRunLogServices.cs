using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch.RunLogs;

public sealed class AgentRunLogServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IAgentRunLogs, AgentRunLogs>();
        services.AddSingleton<IRunLogAttachments, RunLogAttachments>();
    }
}
