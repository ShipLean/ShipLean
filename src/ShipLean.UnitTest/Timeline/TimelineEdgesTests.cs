using ShipLean.Application.Features.Timeline.Build;
using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport.Timeline;

namespace ShipLean.UnitTest.Timeline;

// The edges builder (C3, D6, D7, D19): link arrows, blocked arrows, wave arrows, wait labels and the critical path.
[TestFixture]
public class TimelineEdgesTests
{
    private static readonly DateTimeOffset T0 = TimelineBarsPartBuilder.Zero;

    private static TimelineBarsPartBuilder Bars() => new();

    private static TimelineFactsBuilder Facts() => new();

    [Test]
    public void Build_WhenSuccessorLink_ShouldDrawDependsEdge()
    {
        var facts = Facts().WithLink("T-1", "T-2", TimelineLinkKind.Successor).Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Task("T-2", "S-1", 15, 20).Build();

        var edge = TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem();

        edge.ShouldBe(new TimelineEdgeDto("T-1", "T-2", TimelineEdgeKind.Depends, TimelineEdgeSource.Link, 5, null, null, null, false));
    }

    [Test]
    public void Build_WhenBlockedByStartedBeforeBlockerEnded_ShouldBeBlockedWithUnblockedAt()
    {
        var facts = Facts()
            .WithLink("T-1", "T-2", TimelineLinkKind.BlockedBy)
            .WithMove("T-2", T0.AddHours(5), "Active", StateCategory.InProgress)
            .WithMove("T-2", T0.AddHours(12), "Active", StateCategory.InProgress)
            .WithMove("T-2", T0.AddHours(30), "Active", StateCategory.InProgress)
            .Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Task("T-2", "S-1", 5, 40).Build();

        var edge = TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem();

        edge.Kind.ShouldBe(TimelineEdgeKind.Blocked);
        edge.Source.ShouldBe(TimelineEdgeSource.Link);
        edge.UnblockedAt.ShouldBe(T0.AddHours(12));
        edge.UnblockedHours.ShouldBe(12);
        edge.WaitHours.ShouldBeNull();
        edge.WaitLabel.ShouldBeNull();
    }

    [Test]
    public void Build_WhenBlockedByStartedAfterBlockerEnded_ShouldBeDepends()
    {
        var facts = Facts().WithLink("T-1", "T-2", TimelineLinkKind.BlockedBy).Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Task("T-2", "S-1", 11, 20).Build();

        TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem().Kind.ShouldBe(TimelineEdgeKind.Depends);
    }

    [Test]
    public void Build_WhenBlockerNeverEnded_ShouldLeaveUnblockedAtNull()
    {
        var facts = Facts()
            .WithLink("T-1", "T-2", TimelineLinkKind.BlockedBy)
            .WithMove("T-2", T0.AddHours(5), "Active", StateCategory.InProgress)
            .Build();
        var bars = Bars().Task("T-1", "S-1", 0, null).Task("T-2", "S-1", 5, null).Build(finished: false);

        var edge = TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem();

        edge.Kind.ShouldBe(TimelineEdgeKind.Blocked);
        edge.UnblockedAt.ShouldBeNull();
        edge.UnblockedHours.ShouldBeNull();
    }

    [Test]
    public void Build_WhenBlockedAndNeverMovedBack_ShouldLeaveUnblockedAtNull()
    {
        var facts = Facts()
            .WithLink("T-1", "T-2", TimelineLinkKind.BlockedBy)
            .WithMove("T-2", T0.AddHours(5), "Active", StateCategory.InProgress)
            .Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Task("T-2", "S-1", 5, null).Build(finished: false);

        TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem().UnblockedAt.ShouldBeNull();
    }

    [Test]
    public void Build_WhenLinkEndsOutsideTheBars_ShouldDrawNothing()
    {
        var facts = Facts().WithLink("T-1", "OTHER-1").Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Build();

        TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldBeEmpty();
    }

    [TestCase(13, "13 h")]
    [TestCase(12, "12 h")]
    [TestCase(22.8, "22.8 h")]
    [TestCase(23, "23 h")]
    [TestCase(24, "1 days")]
    [TestCase(25.3, "1.1 days")]
    [TestCase(95, "4 days")]
    public void Build_WhenWaitOver12Hours_ShouldLabelIt(double wait, string label)
    {
        var facts = Facts().WithLink("T-1", "T-2").Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Task("T-2", "S-1", 10 + wait, 200).Build();

        var edge = TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem();

        edge.WaitHours.ShouldBe(wait);
        edge.WaitLabel.ShouldBe(label);
    }

    [TestCase(11.9)]
    [TestCase(0)]
    [TestCase(-3)]
    public void Build_WhenWaitUnder12Hours_ShouldNotLabel(double wait)
    {
        var facts = Facts().WithLink("T-1", "T-2").Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Task("T-2", "S-1", 10 + wait, 200).Build();

        var edge = TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem();

        edge.WaitHours.ShouldBe(wait);
        edge.WaitLabel.ShouldBeNull();
    }

    [Test]
    public void Build_WhenWavesWithoutLinks_ShouldAddOneWaveEdgeFromLastEnded()
    {
        var facts = Facts().WithWave("S-1", "0").WithWave("S-2", "1").Build();
        var bars = Bars()
            .Task("T-1", "S-1", 0, 10).Task("T-2", "S-1", 0, 30).Task("T-3", "S-1", 0, 90)
            .Task("T-4", "S-2", 40, 60)
            .Build();

        var edge = TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem();

        edge.ShouldBe(new TimelineEdgeDto("T-2", "T-4", TimelineEdgeKind.Depends, TimelineEdgeSource.Wave, 10, null, null, null, false));
    }

    [Test]
    public void Build_WhenWaveEdgeTiesOnEnd_ShouldPickLowerKey()
    {
        var facts = Facts().WithWave("S-1", "0").WithWave("S-2", "1").Build();
        var bars = Bars().Task("T-2", "S-1", 0, 30).Task("T-1", "S-1", 0, 30).Task("T-4", "S-2", 40, 60).Build();

        TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem().From.ShouldBe("T-1");
    }

    [Test]
    public void Build_WhenLinkAlreadyJoins_ShouldNotAddWaveEdge()
    {
        var facts = Facts().WithWave("S-1", "0").WithWave("S-2", "1").WithLink("T-1", "T-4").Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Task("T-2", "S-1", 0, 30).Task("T-4", "S-2", 40, 60).Build();

        var edge = TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem();

        edge.Source.ShouldBe(TimelineEdgeSource.Link);
    }

    [Test]
    public void Build_WhenEarlierWaveHasNothingEnded_ShouldSkipToTheWaveBefore()
    {
        var facts = Facts().WithWave("S-0", "0").WithWave("S-1", "1").WithWave("S-2", "2").Build();
        var bars = Bars()
            .Task("T-0", "S-0", 0, 10)
            .Task("T-1", "S-1", 5, null)
            .Task("T-2", "S-2", 40, 60)
            .Build();

        var edges = TimelineEdges.Build(facts, bars, finished: false).Edges;

        edges.ShouldContain(e => e.From == "T-0" && e.To == "T-2" && e.Source == TimelineEdgeSource.Wave);
        edges.ShouldNotContain(e => e.From == "T-1");
    }

    [Test]
    public void Build_WhenSubWaveZeroPointOne_ShouldOrderBetweenWaveZeroAndOne()
    {
        var facts = Facts().WithWave("S-0", "0").WithWave("S-01", "0.1").WithWave("S-1", "1").Build();
        var bars = Bars().Task("T-0", "S-0", 0, 10).Task("T-01", "S-01", 12, 20).Task("T-1", "S-1", 25, 30).Build();

        var edges = TimelineEdges.Build(facts, bars, finished: false).Edges;

        edges.Select(e => (e.From, e.To)).ShouldBe([("T-0", "T-01"), ("T-01", "T-1")]);
    }

    [Test]
    public void Build_WhenTaskNeverStarted_ShouldAddNoWaveEdge()
    {
        var facts = Facts().WithWave("S-1", "0").WithWave("S-2", "1").Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Task("T-2", "S-2", null, null).Build(finished: false);

        TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldBeEmpty();
    }

    [Test]
    public void Build_WhenEndpointIsStory_ShouldUseBracket()
    {
        var facts = Facts().WithLink("S-1", "T-3").Build();
        var bars = Bars().Story("S-1", 0, 20).Task("T-1", "S-1", 0, 20).Task("T-3", "S-2", 50, 60).Build();

        var edge = TimelineEdges.Build(facts, bars, finished: false).Edges.ShouldHaveSingleItem();

        edge.WaitHours.ShouldBe(30);
    }

    // The SHIP-99 shape: 191 -> 192 -> 194 -> 195 -> 196, with 199 blocking 196 part-way.
    private static (TimelineFacts Facts, TimelineBarsPart Bars) Ship99(bool finished)
    {
        var facts = Facts()
            .WithLink("T-191", "T-192").WithLink("T-191", "T-193").WithLink("T-192", "T-194").WithLink("T-193", "T-194")
            .WithLink("T-194", "T-195").WithLink("T-195", "T-196").WithLink("T-199", "T-196", TimelineLinkKind.BlockedBy)
            .WithMove("T-196", T0.AddHours(347), "Active", StateCategory.InProgress)
            .WithMove("T-196", T0.AddHours(374.7), "Active", StateCategory.InProgress)
            .Build();
        var bars = Bars()
            .Task("T-191", "S-185", 9.2, 11.7).Task("T-192", "S-185", 34.5, 59.3).Task("T-193", "S-185", 34.7, 37.2)
            .Task("T-194", "S-186", 81, 184).Task("T-195", "S-186", 229, 252).Task("T-196", "S-186", 347, 376)
            .Task("T-199", "S-189", 369, 370.3)
            .Build(finished);
        return (facts, bars);
    }

    [Test]
    public void Build_WhenFinished_ShouldWalkCriticalPathAndAddBlocker()
    {
        var (facts, bars) = Ship99(finished: true);

        var part = TimelineEdges.Build(facts, bars, finished: true);

        part.CriticalPath.ShouldBe(["T-191", "T-192", "T-194", "T-195", "T-196", "T-199"]);
        part.Edges.Where(e => e.OnCriticalPath).Select(e => (e.From, e.To)).ShouldBe(
            [("T-191", "T-192"), ("T-192", "T-194"), ("T-194", "T-195"), ("T-195", "T-196"), ("T-199", "T-196")]);
        var blocked = part.Edges.Single(e => e.Kind == TimelineEdgeKind.Blocked);
        blocked.UnblockedHours.ShouldBe(374.7);
        blocked.WaitHours.ShouldBeNull();
    }

    [Test]
    public void Build_WhenRunning_ShouldHaveNoCriticalPath()
    {
        var (facts, bars) = Ship99(finished: false);

        var part = TimelineEdges.Build(facts, bars, finished: false);

        part.CriticalPath.ShouldBeEmpty();
        part.Edges.ShouldNotContain(e => e.OnCriticalPath);
    }

    [Test]
    public void Build_WhenTiedEnds_ShouldPickLowerKey()
    {
        var facts = Facts().WithLink("T-1", "T-9").WithLink("T-2", "T-9").Build();
        var bars = Bars().Task("T-2", "S-1", 0, 10).Task("T-1", "S-1", 0, 10).Task("T-9", "S-1", 11, 20).Build();

        TimelineEdges.Build(facts, bars, finished: true).CriticalPath.ShouldBe(["T-1", "T-9"]);
    }

    [Test]
    public void Build_WhenLatestEndIsInferred_ShouldStartTheWalkAtTheLatestRealBar()
    {
        var facts = Facts().Build();
        var bars = Bars().Task("T-1", "S-1", 0, 10).Task("T-2", "S-1", null, 50, inferred: true).Build();

        TimelineEdges.Build(facts, bars, finished: true).CriticalPath.ShouldBe(["T-1"]);
    }
}
