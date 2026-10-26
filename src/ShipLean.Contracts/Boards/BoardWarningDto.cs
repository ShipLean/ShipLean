using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Boards;

public sealed record BoardWarningDto(BoardWarningKind Kind, WorkItemTypeId Type, StateRef State, string Message);
