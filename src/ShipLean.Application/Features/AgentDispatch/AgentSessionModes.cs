using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

// agent_session.mode on the wire and in the table.
internal static class AgentSessionModes
{
    public const string Implement = "implement";
    public const string Propose = "propose";

    public static string ToWire(AgentSessionMode mode) => mode == AgentSessionMode.Propose ? Propose : Implement;
}
