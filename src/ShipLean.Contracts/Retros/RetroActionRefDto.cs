using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroActionRefDto(Guid WorkItemId, string Key, string Title, StateRef State, SprintRef? Sprint, decimal? Points, RetroFixType? FixType, int PointCount);
