using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// ICurrentAgent's one registration (it replaced the always-none default, which could not see the request).
public sealed class RequestCurrentAgentServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddHttpContextAccessor();
        services.AddSingleton<ICurrentAgent, RequestCurrentAgent>();
    }
}
