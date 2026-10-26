using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.WorkItems;

public sealed record ParentRefDto(Guid Id, string Key, TypeRef Type, string Title);
