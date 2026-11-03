using ShipLean.Contracts.Retros;

namespace ShipLean.Contracts.Retros.Decide;

// Existing is the work item id of the live action to add the points to; null creates a new one.
public sealed record DecidePlanAction(string Title, RetroFixType FixType, IReadOnlyList<Guid> PointIds, string? Theme, Guid? Existing);
