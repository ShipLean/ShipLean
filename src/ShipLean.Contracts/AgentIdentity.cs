namespace ShipLean.Contracts;

// A plain ICurrentAgent value. None is the "no agent acting" value, for code that builds a store by hand and has no
// agent to stamp.
public sealed record AgentIdentity(string? Name, string? Host, bool Verified) : ICurrentAgent
{
    public static AgentIdentity None { get; } = new(null, null, false);
}
