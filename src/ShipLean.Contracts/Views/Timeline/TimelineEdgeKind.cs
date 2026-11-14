namespace ShipLean.Contracts.Views.Timeline;

// Depends: From had to finish before To started. Blocked: From blocked To part-way.
public enum TimelineEdgeKind
{
    Depends,
    Blocked,
}
