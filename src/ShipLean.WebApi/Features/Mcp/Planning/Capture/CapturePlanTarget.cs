namespace ShipLean.WebApi.Features.Mcp.Planning.Capture;

// An existing Feature a plan lands under, as resolved for the call.
public sealed record CapturePlanTarget(Guid Id, string Key);
