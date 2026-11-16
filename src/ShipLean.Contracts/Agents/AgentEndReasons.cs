namespace ShipLean.Contracts.Agents;

// agent_session.end_reason values (C2): why a session that is not live ended. camelCase, like the session states. NULL while
// the session is live; unknown is written only by the back-fill, for sessions that ended before reasons were recorded.
public static class AgentEndReasons
{
    public const string Completed = "completed";
    public const string Canceled = "canceled";
    public const string DeliveryFailed = "deliveryFailed";
    public const string AgentError = "agentError";
    public const string Stale = "stale";
    public const string ProposalWithoutIssue = "proposalWithoutIssue";
    public const string Unknown = "unknown";

    public static readonly IReadOnlyList<string> All = [Completed, Canceled, DeliveryFailed, AgentError, Stale, ProposalWithoutIssue, Unknown];
}
