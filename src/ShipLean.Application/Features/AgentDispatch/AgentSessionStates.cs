namespace ShipLean.Application.Features.AgentDispatch;

// The agent_session.state values. Live ones hold the item's one-session slot (ux_agent_session_live).
internal static class AgentSessionStates
{
    public const string Submitted = "submitted";
    public const string Working = "working";
    public const string InputRequired = "inputRequired";
    public const string Completed = "completed";
    public const string Failed = "failed";
    public const string Canceled = "canceled";
    public const string Stale = "stale";

    // The SQL list of live states, for queries that must agree with the partial unique index.
    public const string LiveSql = "('submitted', 'working', 'inputRequired')";
}
