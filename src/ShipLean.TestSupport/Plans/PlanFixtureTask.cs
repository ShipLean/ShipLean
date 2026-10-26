using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Waves;

namespace ShipLean.TestSupport.Plans;

// One task of a plan fixture. Model is a role word (strong, fast), never a model's name.
public sealed record PlanFixtureTask(WaveLabel Wave, string? Lane, string Title, string Brief, string Model, int ContextK)
{
    // The tags capture_plan writes on the task: its story's wave and lane, its own model and ctx.
    public PlanTaskTags Tags => new(Wave, Lane, Model, ContextK);
}
