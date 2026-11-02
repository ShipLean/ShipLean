using ShipLean.Contracts.Retros;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// Existing is the work item id of the action to add the points to; null creates a new one.
public sealed record DecidePlanAction(string Title, RetroFixType FixType, IReadOnlyList<Guid> PointIds, string? Theme, Guid? Existing);
