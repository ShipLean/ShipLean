using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Application.Features.Timeline;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Timeline;

// The pinned fixtures' arrows against the builder (D4): the finished view's edges, critical path and each bar's onCriticalPath are
// exactly what the whole build makes from the fixture's facts, and no arrow in either fixture starts or ends at a story or bug.
[TestFixture]
public class TimelineEdgesFixtureTests
{
    private static JsonObject Built() =>
        JsonSerializer.SerializeToNode(
            TimelineBuilder.Build(TimelineFixtureFacts.Build(), TimelineFixtureFacts.AsOf, TimeZoneInfo.Utc), BoardDtoFixture.Options)!.AsObject();

    private static JsonObject Fixture(string file) => BoardDtoFixture.Read("views", file).AsObject();

    private static string Diff(JsonNode actual, JsonNode expected, string what) =>
        JsonNode.DeepEquals(actual, expected)
            ? string.Empty
            : $"{what} differs from the fixture.\nactual:   {actual.ToJsonString()}\nexpected: {expected.ToJsonString()}";

    [Test]
    public void Build_WhenFixtureFacts_ShouldMakeTheFinishedFixtureEdgesAndCriticalPath()
    {
        var built = Built();
        var fixture = Fixture("feature-timeline-view.json");

        Diff(built["edges"]!, fixture["edges"]!, "edges").ShouldBeEmpty();
        Diff(built["criticalPath"]!, fixture["criticalPath"]!, "criticalPath").ShouldBeEmpty();
    }

    [Test]
    public void Build_WhenFixtureFacts_ShouldMarkTheFixtureBarsOnTheCriticalPath()
    {
        var built = Built()["bars"]!.AsArray().Select(b => (b!["key"]!.GetValue<string>(), b["onCriticalPath"]!.GetValue<bool>()));
        var fixture = Fixture("feature-timeline-view.json")["bars"]!.AsArray().Select(b => (b!["key"]!.GetValue<string>(), b["onCriticalPath"]!.GetValue<bool>()));

        built.ShouldBe(fixture);
    }

    [Test]
    public void Build_WhenFixtureFacts_ShouldMakeTheWholeFinishedFixture()
    {
        Diff(Built(), Fixture("feature-timeline-view.json"), "the view").ShouldBeEmpty();
    }

    [TestCase("feature-timeline-view.json")]
    [TestCase("feature-timeline-running-view.json")]
    public void Edges_WhenFixture_ShouldJoinTasksOnly(string file)
    {
        var fixture = Fixture(file);
        var parents = fixture["stories"]!.AsArray().Select(s => s!["key"]!.GetValue<string>())
            .Concat(fixture["notStarted"]!.AsArray().Where(n => n!["parent"] is null).Select(n => n!["key"]!.GetValue<string>()))
            .ToHashSet(StringComparer.Ordinal);
        var edges = fixture["edges"]!.AsArray();

        edges.ShouldNotBeEmpty();
        edges.ShouldAllBe(e => !parents.Contains(e!["from"]!.GetValue<string>()) && !parents.Contains(e["to"]!.GetValue<string>()));
    }
}
