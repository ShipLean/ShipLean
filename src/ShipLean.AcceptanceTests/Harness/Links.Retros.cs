namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // A sprint's Retro tab: /p/:key/sprints/:sprintId/retro, as routes.ts builds it. Optional: decide=open, point=, view=item, writtenBy=agents|people.
    public static string SprintRetro(string projectKey, Guid sprintId, params (string Key, string? Value)[] parameters) =>
        $"/p/{Segment(projectKey)}/sprints/{Segment(sprintId.ToString())}/retro" + QueryOf(parameters);

    // A sprint's Demo tab: /p/:key/sprints/:sprintId/demo, as routes.ts builds it.
    public static string SprintDemo(string projectKey, Guid sprintId) =>
        $"/p/{Segment(projectKey)}/sprints/{Segment(sprintId.ToString())}/demo";

    // Retrospectives by Feature: /p/:key/retros/features.
    public static string RetroFeatures(string projectKey) => $"/p/{Segment(projectKey)}/retros/features";

    // One item's retro screen: /p/:key/retros/items/:itemKey, ?retro=&decide=open&point=.
    public static string RetroItem(string projectKey, string itemKey, params (string Key, string? Value)[] parameters) =>
        $"/p/{Segment(projectKey)}/retros/items/{Segment(itemKey)}" + QueryOf(parameters);

    // Retrospectives search: /p/:key/retros, with the filters as the query (a null or empty filter is left out).
    public static string Retros(string projectKey, params (string Key, string? Value)[] filters) =>
        $"/p/{Segment(projectKey)}/retros" + QueryOf(filters);

    // Retrospective insights: /p/:key/retros/insights, ?view=themes|actions|impact&range=6|3|all|w6|w12.
    public static string RetroInsights(string projectKey, params (string Key, string? Value)[] parameters) =>
        $"/p/{Segment(projectKey)}/retros/insights" + QueryOf(parameters);

    private static string QueryOf((string Key, string? Value)[] parameters)
    {
        var given = parameters.Where(p => !string.IsNullOrEmpty(p.Value)).ToArray();
        return given.Length == 0 ? "" : Query(given);
    }
}
