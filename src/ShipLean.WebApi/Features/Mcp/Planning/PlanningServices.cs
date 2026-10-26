using ShipLean.Contracts;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning;

// The planning prompts (Program.cs registers only the tools) and the plan shape rules capture_plan checks.
public sealed class PlanningServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddMcpServer().WithPrompts<PlanPrompts>();
        services.AddSingleton<IPlanShapeRules, PlanShapeRules>();
    }
}
