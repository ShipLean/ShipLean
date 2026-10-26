using ShipLean.Application.Features.Mcp.Planning.Tags;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Application.Features.Mcp.Planning.Waves;
using ShipLean.Contracts.Refs;

namespace ShipLean.UnitTest.Mcp.Planning;

[TestFixture]
public class PlanWavesTests
{
    private static PlanStory Story(string key, string wave, string? lane, StateCategory category)
        => new(Guid.NewGuid(), key, $"Story {key}", WaveLabel.Parse(wave), lane, category.ToString(), category, null, key,
            [new PlanTask(Guid.NewGuid(), key + "-T", "Task", "Brief", "New", StateCategory.Proposed, null, new PlanTaskTags(), false)]);

    private static PlanTree Tree(params PlanStory[] stories)
        => new(new PlanFeature(Guid.NewGuid(), "SHIP", "SHIP-1", "Feature", "", "## Contracts", "feat/x"), stories);

    [Test]
    public void Group_ShouldOrderWavesAsDottedNumbersAndKeepStoryOrder()
    {
        var tree = Tree(
            Story("SHIP-5", "2", null, StateCategory.Proposed),
            Story("SHIP-3", "1", "F", StateCategory.Completed),
            Story("SHIP-2", "0.2", null, StateCategory.Completed),
            Story("SHIP-4", "1", "A", StateCategory.Removed),
            Story("SHIP-1", "0.1", null, StateCategory.Completed));

        var waves = PlanWaves.Group(tree);

        waves.Select(w => w.Wave.Text).ShouldBe(["0.1", "0.2", "1", "2"]);
        waves[2].Stories.Select(s => s.Key).ShouldBe(["SHIP-3", "SHIP-4"]);
        waves.Select(w => w.Done).ShouldBe([true, true, true, false]);
    }

    [Test]
    public void PlanWaves_Startable_ShouldBeFirstWaveWithAnOpenStory()
    {
        var tree = Tree(
            Story("SHIP-1", "0.1", null, StateCategory.Completed),
            Story("SHIP-2", "1", "F", StateCategory.Completed),
            Story("SHIP-3", "1", "A", StateCategory.Resolved),
            Story("SHIP-4", "2", null, StateCategory.Proposed));

        PlanWaves.Startable(tree).ShouldBe(WaveLabel.Parse("1"));
    }

    [Test]
    public void PlanWaves_AllDone_ShouldBeNull()
    {
        var tree = Tree(
            Story("SHIP-1", "0.1", null, StateCategory.Completed),
            Story("SHIP-2", "1", null, StateCategory.Removed));

        PlanWaves.Startable(tree).ShouldBeNull();
        PlanWaves.Startable(Tree()).ShouldBeNull();
    }
}
