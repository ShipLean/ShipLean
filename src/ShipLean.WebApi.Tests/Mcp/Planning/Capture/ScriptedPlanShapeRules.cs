using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Capture;

// A test double for the plan shape rules: answers whatever findings a test set, so capture_plan's handling of errors and
// warnings doesn't depend on which rules exist.
public sealed class ScriptedPlanShapeRules : IPlanShapeRules
{
    public IReadOnlyList<PlanFinding> Findings { get; set; } = [];

    public IReadOnlyList<PlanFinding> Check(CapturePlanInput plan) => Findings;
}
