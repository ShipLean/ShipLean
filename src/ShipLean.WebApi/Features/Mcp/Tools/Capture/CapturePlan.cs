namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

// What capture_work seals: the items to create, in order.
public sealed record CapturePlan(IReadOnlyList<PlannedItem> Items);
