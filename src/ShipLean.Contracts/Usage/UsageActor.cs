namespace ShipLean.Contracts.Usage;

// Who did it: a person, an agent acting for a person, or the system. Name and host of an agent are client-supplied, so they are cut to 64 characters.
public sealed record UsageActor(UsageActorKind Kind, Guid? UserId, string? AgentId, string? AgentName, string? AgentHost, bool AgentVerified)
{
    private const int MaxClientTextLength = 64;

    public static UsageActor System { get; } = new(UsageActorKind.System, null, null, null, null, false);

    // "user:<guid>" | "agent:<authorizationId>" | "agent:gw-<userId>" (gateway) | "system"
    public string DistinctId => Kind switch
    {
        UsageActorKind.User => $"user:{UserId}",
        UsageActorKind.Agent => $"agent:{AgentId}",
        _ => "system",
    };

    public static UsageActor ForUser(Guid userId) => new(UsageActorKind.User, userId, null, null, null, false);

    // agentId is the agent authorization id, or "gw-<userId>" for a gateway caller that has none.
    public static UsageActor ForAgent(Guid userId, string agentId, string? name, string? host, bool verified)
        => new(UsageActorKind.Agent, userId, agentId, Cut(name), Cut(host), verified);

    private static string? Cut(string? value) => value is { Length: > MaxClientTextLength } ? value[..MaxClientTextLength] : value;
}
