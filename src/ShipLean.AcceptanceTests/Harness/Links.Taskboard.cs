namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // A sprint's Taskboard: /p/:key/sprints/:sprintId/taskboard, as routes.ts builds it.
    public static string Taskboard(string projectKey, Guid sprintId) =>
        $"/p/{Segment(projectKey)}/sprints/{Segment(sprintId.ToString())}/taskboard";

    // A Feature's board: /p/:key/items/:itemKey/board, as routes.ts builds it.
    public static string FeatureBoard(string projectKey, string itemKey) =>
        $"{WorkItem(projectKey, itemKey)}/board";
}
