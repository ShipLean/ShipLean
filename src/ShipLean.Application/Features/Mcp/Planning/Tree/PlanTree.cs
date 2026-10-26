namespace ShipLean.Application.Features.Mcp.Planning.Tree;

// A plan as it stands in the work items: the Feature and its stories (every live Story child), ordered by
// wave then rank, each with its tasks by rank. IPlanReads builds it; PlanWaves groups it.
public sealed record PlanTree(PlanFeature Feature, IReadOnlyList<PlanStory> Stories);
