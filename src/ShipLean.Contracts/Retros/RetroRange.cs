namespace ShipLean.Contracts.Retros;

// The Insights window: the last 6 sprints, the last 3, all, or the last 6 or 12 weeks (query values 6, 3, all, w6, w12).
public enum RetroRange
{
    Last6,
    Last3,
    All,
    Weeks6,
    Weeks12,
}
