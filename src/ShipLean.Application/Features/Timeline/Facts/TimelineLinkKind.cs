namespace ShipLean.Application.Features.Timeline.Facts;

// The link a TimelineLinkFact came from. Whatever the kind, the fact is normalised so that From must finish before To.
public enum TimelineLinkKind
{
    Predecessor,
    Successor,
    BlockedBy,
}
