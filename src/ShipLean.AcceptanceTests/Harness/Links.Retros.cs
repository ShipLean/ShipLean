namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // A sprint's Retro tab: /p/:key/sprints/:sprintId/retro, as routes.ts builds it.
    public static string SprintRetro(string projectKey, Guid sprintId) =>
        $"/p/{Segment(projectKey)}/sprints/{Segment(sprintId.ToString())}/retro";

    // Retrospectives search: /p/:key/retros, with the filters as the query (a null or empty filter is left out).
    public static string Retros(string projectKey, params (string Key, string? Value)[] filters) =>
        $"/p/{Segment(projectKey)}/retros" + QueryOf(filters);

    // Retrospective insights: /p/:key/retros/insights, ?view=themes|actions|impact&range=6|3|all.
    public static string RetroInsights(string projectKey, params (string Key, string? Value)[] parameters) =>
        $"/p/{Segment(projectKey)}/retros/insights" + QueryOf(parameters);

    private static string QueryOf((string Key, string? Value)[] parameters)
    {
        var given = parameters.Where(p => !string.IsNullOrEmpty(p.Value)).ToArray();
        return given.Length == 0 ? "" : Query(given);
    }
}
