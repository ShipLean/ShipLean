using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

// agent_session.mode on the wire and in the table, and the agent-name stamp of a comment mirrored from GitHub
// (ICommentStore.AddAsAgentAsync(..., agentName: "GitHub @login", ...)), the only comments a propose session reads.
internal static class AgentSessionModes
{
    public const string Implement = "implement";
    public const string Propose = "propose";
    public const string GitHubCommentPrefix = "GitHub @";

    public static string ToWire(AgentSessionMode mode) => mode == AgentSessionMode.Propose ? Propose : Implement;
}
