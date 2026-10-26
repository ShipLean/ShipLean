using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Waves;

namespace ShipLean.TestSupport.Plans;

// One story of a plan fixture; Lane is null for a lane-less wave.
public sealed record PlanFixtureStory(string Title, WaveLabel Wave, string? Lane, string Description, IReadOnlyList<PlanFixtureTask> Tasks)
{
    // The tags capture_plan writes on the story: wave and lane.
    public PlanTaskTags Tags => new(Wave, Lane);
}
