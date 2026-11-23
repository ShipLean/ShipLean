using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Application.Features.Timeline.Build;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Timeline;

// The bars builder against the pinned finished view (feature-timeline-view.json): the facts that story is told from go in, and
// everything the bars part owns must come out as the fixture has it, labels included. Edges, criticalPath, links and each bar's
// onCriticalPath belong to the edges builder and the composition: TimelineEdgesFixtureTests compares those.
[TestFixture]
public class TimelineBarsFixtureTests
{
    private static TimelineBarsPart BuildFixtureView() => TimelineBars.Build(TimelineFixtureFacts.Build(), TimelineFixtureFacts.AsOf, TimeZoneInfo.Utc);

    private static JsonNode Fixture(string property) => BoardDtoFixture.Read("views", "feature-timeline-view.json")[property]!.DeepClone();

    private static JsonNode Json<T>(T value) => JsonSerializer.SerializeToNode(value, BoardDtoFixture.Options)!;

    // An empty string means the two match; otherwise it says what differs.
    private static string Diff(JsonNode actual, JsonNode expected, string what) =>
        JsonNode.DeepEquals(actual, expected)
            ? string.Empty
            : $"{what} differs from the fixture.\nactual:   {actual.ToJsonString()}\nexpected: {expected.ToJsonString()}";

    private static string DiffEach(JsonArray actual, JsonArray expected, string what)
    {
        if (actual.Count != expected.Count)
        {
            return $"{what} has {actual.Count} entries, the fixture has {expected.Count}";
        }

        return string.Concat(expected.Select((item, i) => Diff(actual[i]!, item!, $"{what}[{i}] ({item!["key"]})") + "\n")).Trim();
    }

    private static JsonArray WithoutCriticalPath(JsonNode bars)
    {
        foreach (var bar in bars.AsArray())
        {
            bar!.AsObject().Remove("onCriticalPath");
        }

        return bars.AsArray();
    }

    [Test]
    public void Build_ShouldMakeTheHeaderAndFinishedAsTheFixture()
    {
        var part = BuildFixtureView();

        Diff(Json(part.Feature), Fixture("feature"), "feature").ShouldBeEmpty();
        part.Finished.ShouldBe(Fixture("finished").GetValue<bool>());
    }

    [Test]
    public void Build_ShouldMakeTheKeyDatesGapsAndExecutionAsTheFixture()
    {
        var part = BuildFixtureView();

        DiffEach(Json(part.KeyDates).AsArray(), Fixture("keyDates").AsArray(), "keyDates").ShouldBeEmpty();
        Diff(Json(part.Gaps), Fixture("gaps"), "gaps").ShouldBeEmpty();
        Diff(Json(part.Execution), Fixture("execution"), "execution").ShouldBeEmpty();
    }

    [Test]
    public void Build_ShouldMakeTheScaleAsTheFixture()
    {
        var part = BuildFixtureView();

        var actual = Json(part.Scale).AsObject();
        var expected = Fixture("scale").AsObject();
        Diff(actual["days"]!, expected["days"]!, "scale.days").ShouldBeEmpty();
        Diff(actual["sprints"]!, expected["sprints"]!, "scale.sprints").ShouldBeEmpty();
        Diff(actual, expected, "scale").ShouldBeEmpty();
    }

    [Test]
    public void Build_ShouldMakeTheStoriesAsTheFixture()
    {
        DiffEach(Json(BuildFixtureView().Stories).AsArray(), Fixture("stories").AsArray(), "stories").ShouldBeEmpty();
    }

    [Test]
    public void Build_ShouldMakeTheBarsPeriodsAndReviewsAsTheFixture()
    {
        var bars = WithoutCriticalPath(Json(BuildFixtureView().Bars));
        var expected = WithoutCriticalPath(Fixture("bars"));

        DiffEach(bars, expected, "bars").ShouldBeEmpty();
    }

    [Test]
    public void Build_ShouldLeaveNothingNotStartedAsTheFixture()
    {
        Diff(Json(BuildFixtureView().NotStarted), Fixture("notStarted"), "notStarted").ShouldBeEmpty();
    }
}
