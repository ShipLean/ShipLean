using Microsoft.Extensions.DependencyInjection;
using ShipLean.WebApi.Tests.Mcp.Transport;
using ShipLean.WebApi.Features.Mcp.Planning;

namespace ShipLean.WebApi.Tests.Mcp.Planning;

// The real shape rules are wired in, not the pass-all stub.
[TestFixture]
public class PlanShapeRulesRegistrationTests : McpFixture
{
    protected override bool WithProbeTools => false;

    [Test]
    public void PlanShapeRules_ShouldResolveToRealRules()
    {
        using var scope = Host.Services.CreateScope();

        scope.ServiceProvider.GetRequiredService<IPlanShapeRules>().ShouldBeOfType<PlanShapeRules>();
    }
}
