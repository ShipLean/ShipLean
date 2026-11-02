using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

[TestFixture]
public class UsageActorTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");

    [Test]
    public void DistinctId_WhenEachKind_ShouldBeDistinctAndAtMost50Characters()
    {
        var gateway = UsageActor.ForAgent(UserId, $"gw-{UserId}", null, null, false);
        var agent = UsageActor.ForAgent(UserId, Guid.NewGuid().ToString(), "Claude Code", "claude.ai", true);
        UsageActor[] actors = [UsageActor.ForUser(UserId), agent, gateway, UsageActor.System];

        var ids = actors.Select(a => a.DistinctId).ToList();

        ids.Distinct().Count().ShouldBe(4);
        ids.ShouldAllBe(id => id.Length <= 50);
        ids[0].ShouldBe($"user:{UserId}");
        ids[2].ShouldBe($"agent:gw-{UserId}");
        ids[3].ShouldBe("system");
    }

    [Test]
    public void ForAgent_WhenNameAndHostAreLong_ShouldCutThemTo64Characters()
    {
        var actor = UsageActor.ForAgent(UserId, "a", new string('n', 100), new string('h', 100), false);

        actor.AgentName!.Length.ShouldBe(64);
        actor.AgentHost!.Length.ShouldBe(64);
    }

    private static UsageEvent Event(string name, Dictionary<string, object> properties)
        => new(name, UsageSurface.Web, UsageActor.System, null, 0, UsageOutcome.Ok, null, null, "screen", DateTimeOffset.UnixEpoch, properties);

    [Test]
    public void UsageEvent_WhenValid_ShouldBuild()
    {
        Event(UsageEventNames.ScreenViewed, new() { [UsageProps.Screen] = "login", [UsageProps.Count] = 3L, [UsageProps.Board] = true }).Name.ShouldBe("screen.viewed");
    }

    [Test]
    public void UsageEvent_WhenUnknownName_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => Event("nope.nothing", []));
    }

    [Test]
    public void UsageEvent_WhenUnknownPropertyKey_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => Event(UsageEventNames.ScreenViewed, new() { ["title"] = "x" }));
    }

    [Test]
    public void UsageEvent_WhenStringOver200_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => Event(UsageEventNames.ScreenViewed, new() { [UsageProps.Screen] = new string('x', 201) }));
    }

    [Test]
    public void UsageEvent_WhenValueIsAnObject_ShouldThrow()
    {
        Should.Throw<ArgumentException>(() => Event(UsageEventNames.ScreenViewed, new() { [UsageProps.Screen] = new object() }));
    }
}
