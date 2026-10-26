using ShipLean.Contracts.Boards;

namespace ShipLean.Application.Features.Boards.View;

// Which work item types sit on a level board (database spelling).
public static class BoardLevels
{
    // Every work item type, for reading one card whichever level it is on.
    public static IReadOnlyList<string> AllTypes { get; } = ["epic", "feature", "story", "bug", "task"];

    public static IReadOnlyList<string> TypesOf(BoardLevel level) => level switch
    {
        BoardLevel.Stories => ["story", "bug"],
        BoardLevel.Features => ["feature"],
        BoardLevel.Epics => ["epic"],
        _ => [],
    };
}
