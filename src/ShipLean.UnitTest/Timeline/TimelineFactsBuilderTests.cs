using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport.Timeline;

namespace ShipLean.UnitTest.Timeline;

// The timeline builder's test double (C3): every fact a test sets reaches the facts it builds, and what it doesn't set defaults.
[TestFixture]
public class TimelineFactsBuilderTests
{
    private static readonly DateTimeOffset T0 = TimelineFactsBuilder.Start;
    private static readonly Guid Mei = new("00000000-0000-4000-8000-0000000000a1");

    [Test]
    public void Build_ShouldCarryEveryFactSet()
    {
        var facts = new TimelineFactsBuilder()
            .WithProjectKey("PART")
            .WithFeature("PART-9", "Invoices", "Done", StateCategory.Completed, T0, Mei)
            .WithEpic("PART-1", "Billing")
            .WithPlanApplied(T0.AddHours(2))
            .WithStory("PART-10", "Story ten", Mei, T0.AddHours(1), "Active", StateCategory.InProgress)
            .WithBug("PART-11", "Bug eleven")
            .WithTask("PART-12", "PART-10", "Task twelve", Mei, "Active", StateCategory.InProgress)
            .WithMove("PART-12", T0.AddHours(3), "Active", StateCategory.InProgress, "claude", Mei)
            .WithBlockedTag("PART-12", T0.AddHours(4), blockedAfter: true)
            .WithLink("PART-10", "PART-11", TimelineLinkKind.BlockedBy)
            .WithWave("PART-10", "0.1")
            .WithReview("PART-12", T0.AddHours(5), ReviewVerdict.ChangesRequested, Mei, "codex", "Fix the test")
            .WithSprint("Sprint 7", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 18))
            .WithPerson(Mei, "Mei")
            .Build();

        facts.ProjectKey.ShouldBe("PART");
        facts.Feature.ShouldBe(new TimelineFeatureFact(
            TimelineFactsBuilder.FeatureId, "PART-9", "Invoices", "Done", StateCategory.Completed, T0, Mei, new TimelineEpicFact("PART-1", "Billing")));
        facts.PlanAppliedAt.ShouldBe(T0.AddHours(2));
        facts.Stories.ShouldBe([
            new TimelineStoryFact("PART-10", WorkItemTypeId.Story, "Story ten", Mei, T0.AddHours(1), "Active", StateCategory.InProgress),
            new TimelineStoryFact("PART-11", WorkItemTypeId.Bug, "Bug eleven", null, T0, "New", StateCategory.Proposed),
        ]);
        facts.Tasks.ShouldBe([new TimelineTaskFact("PART-12", "PART-10", "Task twelve", Mei, "Active", StateCategory.InProgress)]);
        facts.Moves.ShouldBe([new TimelineMoveFact("PART-12", T0.AddHours(3), 2, "Active", StateCategory.InProgress, "claude", Mei)]);
        facts.BlockedTags.ShouldBe([new TimelineBlockedTagFact("PART-12", T0.AddHours(4), 3, true)]);
        facts.Links.ShouldBe([new TimelineLinkFact("PART-10", "PART-11", TimelineLinkKind.BlockedBy)]);
        facts.Waves.ShouldBe(new Dictionary<string, string> { ["PART-10"] = "0.1" });
        facts.Reviews.ShouldBe([new TimelineReviewFact("PART-12", T0.AddHours(5), ReviewVerdict.ChangesRequested, "Fix the test", Mei, "codex")]);
        facts.Sprints.ShouldBe([new TimelineSprintFact("Sprint 7", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 18))]);
        facts.People.ShouldBe(new Dictionary<Guid, string> { [Mei] = "Mei" });
    }

    [Test]
    public void Build_WhenNothingSet_ShouldBeTheDefaultFeatureWithNothingUnderIt()
    {
        var facts = new TimelineFactsBuilder().Build();

        facts.ProjectKey.ShouldBe("SHIP");
        facts.Feature.ShouldBe(new TimelineFeatureFact(
            TimelineFactsBuilder.FeatureId, "SHIP-99", "Coupons v2", "New", StateCategory.Proposed, TimelineFactsBuilder.Start, null, null));
        facts.PlanAppliedAt.ShouldBeNull();
        facts.Stories.ShouldBeEmpty();
        facts.Tasks.ShouldBeEmpty();
        facts.Moves.ShouldBeEmpty();
        facts.BlockedTags.ShouldBeEmpty();
        facts.Links.ShouldBeEmpty();
        facts.Waves.ShouldBeEmpty();
        facts.Reviews.ShouldBeEmpty();
        facts.Sprints.ShouldBeEmpty();
        facts.People.ShouldBeEmpty();
    }

    [Test]
    public void Build_WhenTaskStateNotGiven_ShouldTakeItsLatestMove()
    {
        var facts = new TimelineFactsBuilder()
            .WithStory("SHIP-1")
            .WithTask("SHIP-2", "SHIP-1")
            .WithMove("SHIP-2", T0.AddHours(1), "Active", StateCategory.InProgress)
            .WithMove("SHIP-2", T0.AddHours(9), "Resolved", StateCategory.Resolved)
            .Build();

        facts.Tasks.ShouldHaveSingleItem().StateName.ShouldBe("Resolved");
        facts.Moves.Select(m => m.Rev).ShouldBe([2, 3]);
        facts.Stories.ShouldHaveSingleItem().StateName.ShouldBe("New");
    }
}
