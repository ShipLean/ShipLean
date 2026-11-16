using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

// agent_session.mode on the wire and in the table.
internal static class AgentSessionModes
{
    public const string Implement = "implement";
    public const string Propose = "propose";
    public const string Demo = "demo";

    public static string ToWire(AgentSessionMode mode) => mode switch
    {
        AgentSessionMode.Implement => Implement,
        AgentSessionMode.Propose => Propose,
        AgentSessionMode.Demo => Demo,
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, "Unknown agent session mode."),
    };
}
