namespace ShipLean.Contracts.Boards;

// The backlog level a board sits over, or the sprint Taskboard. Matches the board.level column.
public enum BoardLevel
{
    Stories,
    Features,
    Epics,
    Taskboard,
}
