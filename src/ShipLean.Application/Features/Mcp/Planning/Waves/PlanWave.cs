using ShipLean.Application.Features.Mcp.Planning.Tree;

namespace ShipLean.Application.Features.Mcp.Planning.Waves;

// The stories of one wave, in the tree's order. Done when every one is Completed or Removed.
public sealed record PlanWave(WaveLabel Wave, IReadOnlyList<PlanStory> Stories, bool Done);
