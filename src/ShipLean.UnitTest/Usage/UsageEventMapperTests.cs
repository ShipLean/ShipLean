using ShipLean.Application.Features.Usage;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Usage;

namespace ShipLean.UnitTest.Usage;

[TestFixture]
public class UsageEventMapperTests
{
    private static readonly Guid UserId = Guid.Parse("11111111-2222-3333-4444-555555555555");
    private static readonly Guid ProjectId = Guid.Parse("66666666-2222-3333-4444-555555555555");
    private static readonly DateTimeOffset At = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    private static readonly PersonLookup.Person Joel = new("Joel", "joel@example.com");

    private static AnalyticsTarget Target(bool sendNames)
        => new(AnalyticsProvider.Umami, new Uri("https://umami.example.com"), "site-key", "ship.example.com", sendNames, "1.2.3");

    private static UsageEvent Event(
        UsageActor actor, string name = UsageEventNames.ProjectSettingsChanged, Guid? projectId = null,
        IReadOnlyDictionary<string, object>? properties = null, string? errorCode = null, long? durationMs = 12)
        => new(name, UsageSurface.Web, actor, projectId, projectId is null ? 0 : 1, UsageOutcome.Ok, errorCode, durationMs, "api:Test.Action", At, properties ?? new Dictionary<string, object>());

    [Test]
    public void Mapper_WhenAgentEvent_ShouldCarryAgentAndOnBehalfUser()
    {
        var actor = UsageActor.ForAgent(UserId, "auth-1", "Claude Code", "claude.ai", true);

        var mapped = UsageEventMapper.Map(Event(actor), Target(sendNames: true), null, Joel);

        mapped.ActorKind.ShouldBe("agent");
        mapped.DistinctId.ShouldBe("agent:auth-1");
        mapped.Properties[AnalyticsProps.ActorKind].ShouldBe("agent");
        mapped.Properties[AnalyticsProps.UserId].ShouldBe(UserId.ToString());
        mapped.Properties[AnalyticsProps.AgentId].ShouldBe("auth-1");
        mapped.Properties[AnalyticsProps.AgentName].ShouldBe("Claude Code");
        mapped.Properties[AnalyticsProps.AgentHost].ShouldBe("claude.ai");
        mapped.Properties[AnalyticsProps.AgentVerified].ShouldBe(true);
        mapped.Person.DistinctId.ShouldBe("agent:auth-1");
        mapped.Person.Properties[AnalyticsProps.PersonKind].ShouldBe("agent");
        mapped.Person.Properties[AnalyticsProps.PersonUserId].ShouldBe(UserId.ToString());
        mapped.Person.Properties[AnalyticsProps.PersonAgentName].ShouldBe("Claude Code");
        mapped.Person.Properties.ShouldNotContainKey(AnalyticsProps.PersonName);
        mapped.Person.Properties.ShouldNotContainKey(AnalyticsProps.PersonEmail);
    }

    [Test]
    public void Mapper_WhenSendNamesOff_ShouldOmitNameAndEmail()
    {
        // The lookup is never consulted when SendNames is off, so the dispatcher passes none; the mapper must not invent one.
        var mapped = UsageEventMapper.Map(Event(UsageActor.ForUser(UserId)), Target(sendNames: false), null, null);

        mapped.Person.Properties.ShouldNotContainKey(AnalyticsProps.PersonName);
        mapped.Person.Properties.ShouldNotContainKey(AnalyticsProps.PersonEmail);
        mapped.Person.Properties[AnalyticsProps.PersonKind].ShouldBe("user");
        mapped.Person.DistinctId.ShouldBe($"user:{UserId}");
    }

    [Test]
    public void Mapper_WhenSendNamesOn_ShouldAddNameAndEmail()
    {
        var mapped = UsageEventMapper.Map(Event(UsageActor.ForUser(UserId)), Target(sendNames: true), null, Joel);

        mapped.Person.Properties[AnalyticsProps.PersonName].ShouldBe("Joel");
        mapped.Person.Properties[AnalyticsProps.PersonEmail].ShouldBe("joel@example.com");
        mapped.Properties.ShouldNotContainKey(AnalyticsProps.PersonName);
        mapped.Properties.ShouldNotContainKey(AnalyticsProps.PersonEmail);
    }

    [Test]
    public void Mapper_WhenProjectKnown_ShouldAddProjectKey()
    {
        var mapped = UsageEventMapper.Map(Event(UsageActor.ForUser(UserId), projectId: ProjectId), Target(sendNames: false), "SHIP", null);

        mapped.ProjectKey.ShouldBe("SHIP");
        mapped.Properties[AnalyticsProps.ProjectKey].ShouldBe("SHIP");
        mapped.Properties[AnalyticsProps.ProjectId].ShouldBe(ProjectId.ToString());
        mapped.Properties[AnalyticsProps.ProjectCount].ShouldBe(1L);
        mapped.Properties[AnalyticsProps.ShipLeanVersion].ShouldBe("1.2.3");
        mapped.Properties[AnalyticsProps.InstanceHost].ShouldBe("ship.example.com");
        mapped.Properties[AnalyticsProps.Surface].ShouldBe("web");
        mapped.Properties[AnalyticsProps.Outcome].ShouldBe("ok");
        mapped.Properties[AnalyticsProps.DurationMs].ShouldBe(12L);
        mapped.Properties[AnalyticsProps.Source].ShouldBe("api:Test.Action");
        mapped.Timestamp.ShouldBe(At);
    }

    [Test]
    public void Mapper_WhenProjectUnknown_ShouldLeaveTheKeyOut()
    {
        var mapped = UsageEventMapper.Map(Event(UsageActor.ForUser(UserId), projectId: ProjectId), Target(sendNames: false), null, null);

        mapped.ProjectKey.ShouldBeNull();
        mapped.Properties.ShouldNotContainKey(AnalyticsProps.ProjectKey);
        mapped.Properties[AnalyticsProps.ProjectId].ShouldBe(ProjectId.ToString());
    }

    [Test]
    public void Mapper_WhenNullValues_ShouldOmitKeys()
    {
        var mapped = UsageEventMapper.Map(Event(UsageActor.System, durationMs: null), Target(sendNames: true), null, null);

        string[] omitted =
        [
            AnalyticsProps.UserId, AnalyticsProps.AgentId, AnalyticsProps.AgentName, AnalyticsProps.AgentHost, AnalyticsProps.AgentVerified,
            AnalyticsProps.ProjectId, AnalyticsProps.ProjectKey, AnalyticsProps.ErrorCode, AnalyticsProps.DurationMs,
        ];
        foreach (var key in omitted)
        {
            mapped.Properties.ShouldNotContainKey(key);
        }
        mapped.Properties[AnalyticsProps.ActorKind].ShouldBe("system");
        mapped.Properties[AnalyticsProps.ProjectCount].ShouldBe(0L);
        mapped.Person.Properties.Keys.ShouldBe([AnalyticsProps.PersonKind]);
    }

    [Test]
    public void Mapper_WhenExtraPropertyNamesAStandardKey_ShouldKeepTheStandardValue()
    {
        IReadOnlyDictionary<string, object> extras = new Dictionary<string, object> { [UsageProps.AgentName] = "Extra Client" };

        var withStandard = UsageEventMapper.Map(
            Event(UsageActor.ForAgent(UserId, "auth-1", "Claude Code", null, false), UsageEventNames.AgentConnected, properties: extras),
            Target(sendNames: false), null, null);
        var withoutStandard = UsageEventMapper.Map(
            Event(UsageActor.ForUser(UserId), UsageEventNames.AgentConnected, properties: extras), Target(sendNames: false), null, null);

        withStandard.Properties[AnalyticsProps.AgentName].ShouldBe("Claude Code");
        withoutStandard.Properties[AnalyticsProps.AgentName].ShouldBe("Extra Client");
    }

    [Test]
    public void Mapper_WhenExtraPropertiesHaveEachType_ShouldCopyThemAndWidenInt()
    {
        IReadOnlyDictionary<string, object> extras = new Dictionary<string, object>
        {
            [UsageProps.Screen] = "board",
            [UsageProps.Count] = 3,
            [UsageProps.Board] = 2.5,
            [UsageProps.Provider] = true,
        };

        var mapped = UsageEventMapper.Map(Event(UsageActor.ForUser(UserId), properties: extras), Target(sendNames: false), null, null);

        mapped.Properties[UsageProps.Screen].ShouldBe("board");
        mapped.Properties[UsageProps.Count].ShouldBe(3L);
        mapped.Properties[UsageProps.Count].ShouldBeOfType<long>();
        mapped.Properties[UsageProps.Board].ShouldBe(2.5);
        mapped.Properties[UsageProps.Provider].ShouldBe(true);
    }
}
