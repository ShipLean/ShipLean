namespace ShipLean.Contracts.Agents;

// The agent_session.state values that hold an item's one live-session slot (ux_agent_session_live), as an SQL list for
// queries outside agent dispatch that must agree with the partial unique index.
public static class AgentSessionLiveStates
{
    public const string Sql = "('submitted', 'working', 'inputRequired')";
}
