using System.Text.Json;
using System.Text.Json.Nodes;
using ShipLean.Application.Features.Timeline.Build;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.TestSupport.Timeline;
using ShipLean.UnitTest.Dtos.Boards;

namespace ShipLean.UnitTest.Timeline;

// The bars builder against the pinned finished view (feature-timeline-view.json): the facts that story is told from go in, and
// everything the bars part owns must come out as the fixture has it, labels included. Edges, criticalPath, links and each bar's
// onCriticalPath belong to the edges builder and the composition, so they are not compared here.
[TestFixture]
public class TimelineBarsFixtureTests
{
    private static readonly Guid Priya = Person(1);
    private static readonly Guid Tom = Person(2);
    private static readonly Guid Mei = Person(3);
    private static readonly Guid Sam = Person(4);
    private static readonly Guid Ana = Person(5);
    private const string Agent = "Claude Code";

    private static Guid Person(int n) => new($"6f1c2a10-0000-4000-8000-00000000000{n}");

    private static DateTimeOffset At(string iso) => DateTimeOffset.Parse($"2026-{iso}:00Z", System.Globalization.CultureInfo.InvariantCulture);

    private static void Active(TimelineFactsBuilder f, string key, string at, string? agent = null) =>
        f.WithMove(key, At(at), "Active", StateCategory.InProgress, agent);

    private static void Resolved(TimelineFactsBuilder f, string key, string at) =>
        f.WithMove(key, At(at), "Resolved", StateCategory.Resolved);

    // The delivery the fixture draws, as facts: four stories and bugs, nine tasks, two blocked spells, ten reviews, two sprints.
    private static TimelineBarsPart BuildFixtureView()
    {
        var f = new TimelineFactsBuilder()
            .WithFeature("SHIP-99", "Coupons v2", "Done", StateCategory.Completed, At("08-18T10:20"), Ana)
            .WithEpic("SHIP-80", "Checkout conversion")
            .WithPerson(Priya, "Priya Shah").WithPerson(Tom, "Tom Okafor").WithPerson(Mei, "Mei Lin").WithPerson(Sam, "Sam Rivera").WithPerson(Ana, "Ana Costa")
            .WithPlanApplied(At("10-01T16:45")).WithPlannedBy(agentName: Agent)
            .WithStory("SHIP-185", "Coupon rules engine", Tom, At("10-01T16:45"))
            .WithStory("SHIP-188", "Coupon admin page", Ana, At("10-01T16:45"))
            .WithStory("SHIP-186", "Apply coupon at checkout", Priya, At("10-01T16:45"))
            .WithBug("SHIP-189", "Coupon applied twice on double-click", Sam, At("10-20T08:00"))
            .WithTask("SHIP-191", "SHIP-185", "Rules schema and migration")
            .WithTask("SHIP-192", "SHIP-185", "Stacking rules (percent + fixed)")
            .WithTask("SHIP-193", "SHIP-185", "Expiry and usage limits")
            .WithTask("SHIP-197", "SHIP-188", "Admin list and create form", Ana)
            .WithTask("SHIP-198", "SHIP-188", "Bulk code import", Ana)
            .WithTask("SHIP-194", "SHIP-186", "API: validate and apply", Priya)
            .WithTask("SHIP-195", "SHIP-186", "UI: coupon field and errors", Mei)
            .WithTask("SHIP-196", "SHIP-186", "E2E: coupon checkout")
            .WithTask("SHIP-199", "SHIP-189", "Debounce apply and make it idempotent")
            .WithSprint("Sprint 41", new DateOnly(2026, 10, 5), new DateOnly(2026, 10, 11))
            .WithSprint("Sprint 42", new DateOnly(2026, 10, 12), new DateOnly(2026, 10, 25));

        Active(f, "SHIP-191", "10-05T09:10", Agent);
        Resolved(f, "SHIP-191", "10-05T11:40");
        Active(f, "SHIP-192", "10-06T10:30", Agent);
        Resolved(f, "SHIP-192", "10-06T14:00");
        Active(f, "SHIP-192", "10-07T09:30");
        Resolved(f, "SHIP-192", "10-07T11:15");
        Active(f, "SHIP-193", "10-06T10:40", Agent);
        Resolved(f, "SHIP-193", "10-06T13:10");
        Active(f, "SHIP-197", "10-06T13:00");
        Resolved(f, "SHIP-197", "10-08T11:00");
        f.WithMove("SHIP-198", At("10-16T17:30"), "Closed", StateCategory.Completed);
        Active(f, "SHIP-194", "10-08T09:00");
        f.WithBlockedTag("SHIP-194", At("10-08T15:00"), blockedAfter: true).WithBlockedTag("SHIP-194", At("10-12T10:00"), blockedAfter: false);
        Resolved(f, "SHIP-194", "10-12T16:00");
        Active(f, "SHIP-195", "10-14T13:00");
        Resolved(f, "SHIP-195", "10-15T12:00");
        Active(f, "SHIP-196", "10-19T11:00", Agent);
        f.WithBlockedTag("SHIP-196", At("10-19T13:30"), blockedAfter: true).WithBlockedTag("SHIP-196", At("10-20T14:40"), blockedAfter: false);
        Resolved(f, "SHIP-196", "10-20T16:00");
        Active(f, "SHIP-199", "10-20T09:00", Agent);
        Resolved(f, "SHIP-199", "10-20T10:20");

        f.WithReview("SHIP-191", At("10-06T10:05"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-192", At("10-07T09:30"), ReviewVerdict.ChangesRequested, Ana, Agent, "Percent and fixed discounts apply in the wrong order when both match.")
            .WithReview("SHIP-192", At("10-07T16:20"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-193", At("10-06T17:00"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-197", At("10-09T11:00"), ReviewVerdict.Approved, Tom)
            .WithReview("SHIP-194", At("10-14T11:30"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-195", At("10-19T10:15"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-196", At("10-21T09:40"), ReviewVerdict.Approved, Ana, Agent)
            .WithReview("SHIP-199", At("10-20T14:30"), ReviewVerdict.Approved, Ana, Agent);
        f.WithMove("SHIP-99", At("10-21T10:05"), "Done", StateCategory.Completed, userId: Priya);

        var facts = f.Build();
        facts = facts with { Feature = facts.Feature with { Id = new Guid("0b6f3c9e-99a0-4c1e-9d55-000000000099") } };
        return TimelineBars.Build(facts, At("10-24T14:00"), TimeZoneInfo.Utc);
    }

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
