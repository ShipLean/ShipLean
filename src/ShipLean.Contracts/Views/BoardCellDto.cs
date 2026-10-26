namespace ShipLean.Contracts.Views;

// One lane x column (x half, for a split column). In the last column Cards holds the most recent few; OlderCount is how
// many more "Show older" reveals and OlderCursor is the opaque cursor to ask for them (null when OlderCount is 0).
public sealed record BoardCellDto(string ColumnId, bool DoneHalf, IReadOnlyList<CardDto> Cards, int OlderCount, string? OlderCursor);
