using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Plan;

public sealed class PlanServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration) => services.AddSingleton<PlanEnvelope>();
}
