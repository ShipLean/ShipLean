using ShipLean.Contracts;
using ShipLean.TestSupport;
using ShipLean.TestSupport.AccessSweep;

namespace ShipLean.WebApi.Tests.Boards;

// The board settings routes. The put sends SHIP's Taskboard document back unchanged, so the allowed leg saves nothing new.
public sealed class BoardSettingsRows : IAccessSweepRows
{
    public IEnumerable<AccessSweepRow> Rows =>
    [
        new("GET", "api/boards/{id:guid}/settings", ProjectRole.Member, Url: $"api/boards/{SeedIds.Boards.ShipTaskboard}/settings"),
        new("PUT", "api/boards/{id:guid}/settings", ProjectRole.Member, Body: PutBody(), Url: $"api/boards/{SeedIds.Boards.ShipTaskboard}/settings"),
        new("GET", "api/projects/{id:guid}/boards", ProjectRole.Member),
    ];

    private static string PutBody()
    {
        var document = File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "PinnedShapes", "board-settings.taskboard.json"));
        return $$"""{"basedOn":{{document}},"settings":{{document}}}""";
    }
}
