namespace ShipLean.Contracts.Views;

public sealed record TaskboardCellDto(Guid StateId, IReadOnlyList<CardDto> Tasks);
