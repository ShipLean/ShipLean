namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // All sprints: /p/:key/sprints, as routes.ts builds it.
    public static string Sprints(string projectKey) => $"/p/{Segment(projectKey)}/sprints";

    // A sprint's backlog: /p/:key/sprints/:sprintId/backlog, as routes.ts builds it.
    public static string SprintBacklog(string projectKey, Guid sprintId) =>
        $"/p/{Segment(projectKey)}/sprints/{Segment(sprintId.ToString())}/backlog";

    // A sprint's capacity: /p/:key/sprints/:sprintId/capacity, as routes.ts builds it.
    public static string SprintCapacity(string projectKey, Guid sprintId) =>
        $"/p/{Segment(projectKey)}/sprints/{Segment(sprintId.ToString())}/capacity";
}
