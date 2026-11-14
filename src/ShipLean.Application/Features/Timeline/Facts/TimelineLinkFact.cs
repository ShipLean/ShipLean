namespace ShipLean.Application.Features.Timeline.Facts;

// A link between two items of this Feature, normalised: From must finish before To. Kind is the link it came from.
public sealed record TimelineLinkFact(string From, string To, TimelineLinkKind Kind);
