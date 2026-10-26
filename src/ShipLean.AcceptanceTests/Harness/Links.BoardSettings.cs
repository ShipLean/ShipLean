namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // A level's board with its settings dialog open on a section ("columns", ...): /p/:key/boards/:level?settings=.
    public static string BoardSettings(string projectKey, string level, string section) =>
        $"/p/{Segment(projectKey)}/boards/{Segment(level)}" + Query(("settings", section));
}
