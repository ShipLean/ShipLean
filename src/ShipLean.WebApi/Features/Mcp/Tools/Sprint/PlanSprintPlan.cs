using ShipLean.WebApi.Features.Mcp.Plan;

namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// What plan_sprint seals: the sprint and the items to move into it, each at the revision the preview saw.
public sealed record PlanSprintPlan(Guid SprintId, IReadOnlyList<PlanItem> Items);
