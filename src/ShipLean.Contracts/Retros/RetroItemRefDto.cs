using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Retros;

public sealed record RetroItemRefDto(Guid Id, string Key, TypeRef Type, string Title);
