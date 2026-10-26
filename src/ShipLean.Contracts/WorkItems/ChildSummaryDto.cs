using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

public sealed record ChildSummaryDto(Guid Id, string Key, TypeRef Type, string Title, StateRef State, UserRef? Assignee, decimal? Points);
