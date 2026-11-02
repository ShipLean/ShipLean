namespace ShipLean.Contracts.Analytics;

// The standard property keys of every AnalyticsEvent. The dispatcher and both sinks use these constants, never literals.
public static class AnalyticsProps
{
    public const string Surface = "surface";
    public const string ActorKind = "actor_kind";
    public const string UserId = "user_id";
    public const string AgentId = "agent_id";
    public const string AgentName = "agent_name";
    public const string AgentHost = "agent_host";
    public const string AgentVerified = "agent_verified";
    public const string ProjectId = "project_id";
    public const string ProjectKey = "project_key";
    public const string ProjectCount = "project_count";
    public const string Outcome = "outcome";
    public const string ErrorCode = "error_code";
    public const string DurationMs = "duration_ms";
    public const string Source = "source";
    public const string ShipLeanVersion = "shiplean_version";
    public const string InstanceHost = "instance_host";

    // Person property keys (the C2 list; name and email only when SendNames is on).
    public const string PersonKind = "kind";
    public const string PersonUserId = "user_id";
    public const string PersonAgentName = "agent_name";
    public const string PersonAgentHost = "agent_host";
    public const string PersonAgentVerified = "agent_verified";
    public const string PersonName = "name";
    public const string PersonEmail = "email";
}
