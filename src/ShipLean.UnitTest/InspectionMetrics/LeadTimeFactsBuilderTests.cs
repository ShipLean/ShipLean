using ShipLean.Contracts.Refs;
using ShipLean.TestSupport.InspectionMetrics;

namespace ShipLean.UnitTest.InspectionMetrics;

// The test double of K5 and its sample (design/screens/inspection-data.js): Sprints 16 to 24, SHIP-194's 3.8 d block, SHIP-192's
// rework and SHIP-198 left out.
[TestFixture]
public class LeadTimeFactsBuilderTests
{
    private static readonly DateTimeOffset T0 = LeadTimeFactsBuilder.Start;

    [Test]
    public void Sample_ShouldMatchDesignCounts()
    {
        var facts = LeadTimeFactsBuilder.Sample();

        LeadTimeFactsBuilder.SampleSprints().Count.ShouldBe(9);
        LeadTimeFactsBuilder.SampleSprints().Select(s => s.Name).First().ShouldBe("Sprint 16");
        LeadTimeFactsBuilder.SampleSprints().Select(s => s.Name).Last().ShouldBe("Sprint 24");
        facts.Tasks.Count(t => t.Key != LeadTimeFactsBuilder.SampleLeftOutKey).ShouldBe(123);
        facts.Tasks.Select(t => t.Key).Distinct().Count().ShouldBe(facts.Tasks.Count);
        facts.Tasks.Single(t => t.Key == LeadTimeFactsBuilder.SampleLeftOutKey).Title.ShouldBe("Bulk code import");
    }

    [Test]
    public void Sample_ShouldKeepTheDesignsHandWrittenTasks()
    {
        var facts = LeadTimeFactsBuilder.Sample();

        var blocked = facts.BlockedTags.Where(b => b.ItemKey == "SHIP-194").OrderBy(b => b.At).ToList();
        (blocked[1].At - blocked[0].At).TotalHours.ShouldBe(91);
        facts.Commits.Where(c => c.ItemKey == "SHIP-192").Count().ShouldBe(2);
        facts.Moves.Single(m => m.ItemKey == "SHIP-194" && m.Category == StateCategory.InProgress).At.ShouldBe(new DateTimeOffset(2026, 10, 8, 9, 0, 0, TimeSpan.Zero));
        facts.Moves.Where(m => m.ItemKey == LeadTimeFactsBuilder.SampleLeftOutKey).Select(m => m.Category).ShouldBe([StateCategory.Resolved]);
    }

    [Test]
    public void Sample_WhenBuiltTwice_ShouldBeTheSameFacts()
    {
        var first = LeadTimeFactsBuilder.Sample();
        var second = LeadTimeFactsBuilder.Sample();

        first.Tasks.ShouldBe(second.Tasks);
        first.Moves.ShouldBe(second.Moves);
        first.Commits.ShouldBe(second.Commits);
    }

    [Test]
    public void Build_WhenMovesGiven_ShouldCountRevisionsPerTaskFromTwo()
    {
        var facts = new LeadTimeFactsBuilder()
            .WithTask("SHIP-1")
            .WithTask("SHIP-2")
            .WithStart("SHIP-1", T0.AddHours(1))
            .WithBlockedTag("SHIP-1", T0.AddHours(2), true)
            .WithStart("SHIP-2", T0.AddHours(3))
            .WithResolved("SHIP-1", T0.AddHours(4))
            .Build();

        facts.Moves.Select(m => (m.ItemKey, m.Rev)).ShouldBe([("SHIP-1", 2), ("SHIP-2", 2), ("SHIP-1", 4)]);
        facts.BlockedTags.Single().Rev.ShouldBe(3);
    }

    [Test]
    public void Build_WhenTaskHasNoGivenState_ShouldTakeTheLatestMove()
    {
        var facts = new LeadTimeFactsBuilder()
            .WithTask("SHIP-1")
            .WithTask("SHIP-2")
            .WithStart("SHIP-1", T0)
            .WithResolved("SHIP-1", T0.AddHours(2))
            .Build();

        facts.Tasks.Select(t => (t.Key, t.StateCategory)).ShouldBe([("SHIP-1", StateCategory.Resolved), ("SHIP-2", StateCategory.Proposed)]);
    }
}
