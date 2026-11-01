namespace ShipLean.Contracts.Usage;

// How the action ended. Wire values: "ok" | "error" | "cancelled".
public enum UsageOutcome
{
    Ok,
    Error,
    Cancelled,
}
