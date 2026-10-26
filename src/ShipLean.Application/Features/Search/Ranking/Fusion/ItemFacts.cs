using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Search.Ranking.Fusion;

// What the boosts and the filters need to know about a fused item.
public sealed record ItemFacts(Guid Id, Guid ProjectId, StateCategory Category, Guid? AssigneeId, DateTimeOffset ChangedAt);
