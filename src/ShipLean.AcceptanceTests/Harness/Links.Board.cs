namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // A level's board: /p/:key/boards/:level.
    public static string Board(string projectKey, string level) =>
        $"/p/{Segment(projectKey)}/boards/{Segment(level)}";
}
