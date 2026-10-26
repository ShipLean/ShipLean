namespace ShipLean.Contracts;

// The agent (an MCP client such as Claude Code) acting for the signed-in user on this request, or no agent when a person
// is using the app. Name is null when no agent is acting. Host is the client's verified host ("claude.ai") for a client
// with a metadata document, null for a self-registered one. Verified says whether Host was checked.
// The stores stamp AgentStamp.Of(agent) on every revision and comment they write.
public interface ICurrentAgent
{
    string? Name { get; }

    string? Host { get; }

    bool Verified { get; }
}
