using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Planning;

// The shape rules a plan is checked against before capture_plan previews it. An error finding refuses the preview; a
// warning rides on it. Pure: the rules see only the plan.
public interface IPlanShapeRules
{
    IReadOnlyList<PlanFinding> Check(CapturePlanInput plan);
}
