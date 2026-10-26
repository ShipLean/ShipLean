namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // A level's backlog, optionally in a mode ("list", "planning", "tree"): /p/:key/backlogs/:level?mode=, as routes.ts builds it.
    public static string Backlog(string projectKey, string level = "stories", string? mode = null) =>
        $"/p/{Segment(projectKey)}/backlogs/{Segment(level)}" + (mode is null ? "" : Query(("mode", mode)));

    // The Jira import of a project: /p/:key/import.
    public static string JiraImport(string projectKey) => $"/p/{Segment(projectKey)}/import";
}
