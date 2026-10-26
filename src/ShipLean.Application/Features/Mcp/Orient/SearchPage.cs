namespace ShipLean.Application.Features.Mcp.Orient;

// The best Cap hits; More says there were others.
public sealed record SearchPage(IReadOnlyList<SearchHit> Hits, bool More);
