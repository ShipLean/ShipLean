using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.Rows;

// A card's state as LinkCardRows stores it: read from the provider (Live) or reported by an agent (Reported, with the agent's stamp).
public sealed record LinkStateWrite(LinkState? State, LinkStateSource Source, DateTimeOffset AsOf, string? ReportedBy);
