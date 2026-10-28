using ShipLean.Application.Features.AgentDispatch.Rendering;

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

    // The ended ones. A stale session is neither live nor terminal: it can be started again, so its queued prompts are kept.
    public const string TerminalSql = "('completed', 'failed', 'canceled')";

    public static bool IsTerminal(string state) => state is Completed or Failed or Canceled;

    // Linear's AgentSessionStatus for a ShipLean state (AgentSessionStatuses).
    public static string ToLinearStatus(string state) => state switch
    {
        Submitted => AgentSessionStatuses.Pending,
        Working => AgentSessionStatuses.Active,
        InputRequired => AgentSessionStatuses.AwaitingInput,
        Completed or Canceled => AgentSessionStatuses.Complete,
        Failed => AgentSessionStatuses.Error,
        Stale => AgentSessionStatuses.Stale,
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, "Not an agent session state."),
    };
}
