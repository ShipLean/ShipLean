using Agoda.IoC.Core;
using ShipLean.Contracts.Analytics;
using ShipLean.Contracts.Usage;

namespace ShipLean.Application.Features.Usage;

// UsageEvent to AnalyticsEvent: the standard properties of the record, the person, and the event's extra properties. A standard
// property whose value is null is left out; an extra property never replaces a standard one.
[RegisterSingleton]
public sealed class UsageEventMapper(ProjectKeyLookup projects, PersonLookup people)
{
    public async Task<AnalyticsEvent> MapAsync(UsageEvent usageEvent, AnalyticsTarget target, CancellationToken cancellationToken)
    {
        var projectKey = usageEvent.ProjectId is { } projectId ? await projects.FindAsync(projectId, cancellationToken) : null;
        PersonLookup.Person? person = null;
        if (target.SendNames && usageEvent.Actor is { Kind: UsageActorKind.User, UserId: { } userId })
        {
            person = await people.FindAsync(userId, cancellationToken);
        }
        return Build(usageEvent, target, projectKey, person);
    }

    public static AnalyticsEvent Build(UsageEvent usageEvent, AnalyticsTarget target, string? projectKey, PersonLookup.Person? person)
    {
        var actor = usageEvent.Actor;
        var isAgent = actor.Kind == UsageActorKind.Agent;
        var properties = new Dictionary<string, object>();
        Add(properties, AnalyticsProps.Surface, SurfaceWire(usageEvent.Surface));
        Add(properties, AnalyticsProps.ActorKind, KindWire(actor.Kind));
        Add(properties, AnalyticsProps.UserId, actor.UserId?.ToString());
        Add(properties, AnalyticsProps.AgentId, actor.AgentId);
        Add(properties, AnalyticsProps.AgentName, actor.AgentName);
        Add(properties, AnalyticsProps.AgentHost, actor.AgentHost);
        Add(properties, AnalyticsProps.AgentVerified, isAgent ? actor.AgentVerified : null);
        Add(properties, AnalyticsProps.ProjectId, usageEvent.ProjectId?.ToString());
        Add(properties, AnalyticsProps.ProjectKey, projectKey);
        Add(properties, AnalyticsProps.ProjectCount, (long)usageEvent.ProjectCount);
        Add(properties, AnalyticsProps.Outcome, OutcomeWire(usageEvent.Outcome));
        Add(properties, AnalyticsProps.ErrorCode, usageEvent.ErrorCode);
        Add(properties, AnalyticsProps.DurationMs, usageEvent.DurationMs);
        Add(properties, AnalyticsProps.Source, usageEvent.Source);
        Add(properties, AnalyticsProps.ShipLeanVersion, target.ShipLeanVersion);
        Add(properties, AnalyticsProps.InstanceHost, target.InstanceHost);
        foreach (var (key, value) in usageEvent.Properties)
        {
            // The standard value wins; int is widened so a sink only ever sees string, long, double or bool.
            properties.TryAdd(key, value is int number ? (long)number : value);
        }

        return new AnalyticsEvent(
            usageEvent.Name, actor.DistinctId, KindWire(actor.Kind), projectKey, usageEvent.OccurredAt, properties, MapPerson(actor, target.SendNames ? person : null));
    }

    private static AnalyticsPerson MapPerson(UsageActor actor, PersonLookup.Person? person)
    {
        var properties = new Dictionary<string, object>();
        Add(properties, AnalyticsProps.PersonKind, KindWire(actor.Kind));
        Add(properties, AnalyticsProps.PersonUserId, actor.UserId?.ToString());
        Add(properties, AnalyticsProps.PersonAgentName, actor.AgentName);
        Add(properties, AnalyticsProps.PersonAgentHost, actor.AgentHost);
        Add(properties, AnalyticsProps.PersonAgentVerified, actor.Kind == UsageActorKind.Agent ? actor.AgentVerified : null);
        if (actor.Kind == UsageActorKind.User)
        {
            Add(properties, AnalyticsProps.PersonName, person?.Name);
            Add(properties, AnalyticsProps.PersonEmail, person?.Email);
        }
        return new AnalyticsPerson(actor.DistinctId, properties);
    }

    private static void Add(Dictionary<string, object> properties, string key, object? value)
    {
        if (value is not null)
        {
            properties[key] = value;
        }
    }

    private static string SurfaceWire(UsageSurface surface) => surface switch
    {
        UsageSurface.Web => "web",
        UsageSurface.Mcp => "mcp",
        UsageSurface.App => "app",
        _ => "system",
    };

    private static string KindWire(UsageActorKind kind) => kind switch
    {
        UsageActorKind.User => "user",
        UsageActorKind.Agent => "agent",
        _ => "system",
    };

    private static string OutcomeWire(UsageOutcome outcome) => outcome switch
    {
        UsageOutcome.Ok => "ok",
        UsageOutcome.Error => "error",
        _ => "cancelled",
    };
}
