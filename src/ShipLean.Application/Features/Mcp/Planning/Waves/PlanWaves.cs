using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Mcp.Planning.Waves;

// Waves of a plan tree. A lead closes its story when its tasks pass review; that is what lets the next wave start.
public static class PlanWaves
{
    // The waves in order, each with its stories in the tree's order.
    public static IReadOnlyList<PlanWave> Group(PlanTree tree)
        => [.. tree.Stories
            .GroupBy(story => story.Wave)
            .OrderBy(wave => wave.Key)
            .Select(wave => new PlanWave(wave.Key, [.. wave], wave.All(IsDone)))];

    // The first wave that is not done, or null when every wave is (or the tree has none).
    public static WaveLabel? Startable(PlanTree tree) => Group(tree).FirstOrDefault(wave => !wave.Done)?.Wave;

    public static bool IsDone(PlanStory story) => story.Category is StateCategory.Completed or StateCategory.Removed;
}
