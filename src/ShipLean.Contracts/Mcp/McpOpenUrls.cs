namespace ShipLean.Contracts.Mcp;

// Absolute URLs of the screens a tool links to, from the instance's base URL ("https://planner.example.com"). The
// paths are the patterns in the client's routes.ts and must stay in step with it (a test reads routes.ts).
public static class McpOpenUrls
{
    public static string WorkItem(string baseUrl, string projectKey, string itemKey)
        => Join(baseUrl, $"/p/{Segment(projectKey)}/items/{Segment(itemKey)}");

    public static string WorkItemAttachment(string baseUrl, string projectKey, string itemKey, Guid attachmentId)
        => Join(baseUrl, $"/p/{Segment(projectKey)}/items/{Segment(itemKey)}/attachments/{Segment(attachmentId.ToString())}");

    // level: stories, features or epics.
    public static string Board(string baseUrl, string projectKey, string level)
        => Join(baseUrl, $"/p/{Segment(projectKey)}/boards/{Segment(level)}");

    // level: stories, features or epics. mode: list, planning or tree; null opens the default (list).
    public static string Backlog(string baseUrl, string projectKey, string level, string? mode = null)
        => Join(baseUrl, $"/p/{Segment(projectKey)}/backlogs/{Segment(level)}" + (mode is null ? "" : $"?mode={Segment(mode)}"));

    public static string Sprints(string baseUrl, string projectKey)
        => Join(baseUrl, $"/p/{Segment(projectKey)}/sprints");

    public static string SprintBacklog(string baseUrl, string projectKey, Guid sprintId)
        => Join(baseUrl, $"/p/{Segment(projectKey)}/sprints/{sprintId}/backlog");

    public static string Taskboard(string baseUrl, string projectKey, Guid sprintId)
        => Join(baseUrl, $"/p/{Segment(projectKey)}/sprints/{sprintId}/taskboard");

    // The Feature board: the Taskboard scoped to one Feature.
    public static string FeatureBoard(string baseUrl, string projectKey, string itemKey)
        => Join(baseUrl, $"/p/{Segment(projectKey)}/items/{Segment(itemKey)}/board");

    // The planning view of the stories backlog.
    public static string Planning(string baseUrl, string projectKey)
        => Backlog(baseUrl, projectKey, "stories", "planning");

    private static string Join(string baseUrl, string path) => baseUrl.TrimEnd('/') + path;

    private static string Segment(string value) => Uri.EscapeDataString(value);
}
