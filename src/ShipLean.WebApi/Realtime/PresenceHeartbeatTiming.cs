namespace ShipLean.WebApi.Realtime;

// How often PresenceHeartbeat beats, and how long an instance's heartbeat lives without one. A crashed instance's
// presence entries go within Ttl plus one Period.
public sealed record PresenceHeartbeatTiming(TimeSpan Period, TimeSpan Ttl)
{
    public static readonly PresenceHeartbeatTiming Default = new(TimeSpan.FromSeconds(15), TimeSpan.FromSeconds(45));
}
