using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Views;

// The Group by people layout. Person is null for the Unassigned row, which comes first. Cells has one entry per column.
public sealed record TaskboardPersonRowDto(UserRef? Person, int TaskCount, IReadOnlyList<TaskboardCellDto> Cells);
