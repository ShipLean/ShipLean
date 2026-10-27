namespace ShipLean.WebApi.Tests.Access;

// The /api routes that are neither swept (no project role to test), nor [InstanceAdmin]: each with the reason. A route
// that needs a project is never listed here; it gets an access sweep row.
public static class RouteAllowList
{
    // A valid sign-in, so the anonymous check sees the route's success, not its own 401 for bad credentials.
    private const string LoginBody = """{"email":"joel@shiplean.local","password":"joel-dev-password"}""";

    public sealed record Entry(string Method, string Template, string Reason, string? Body = null);

    public static IReadOnlyList<Entry> Anonymous { get; } =
    [
        new("POST", "api/auth/login", "Signs a user in.", Body: LoginBody),
        new("POST", "api/auth/logout", "Ends the cookie session, valid or not."),
        new("GET", "api/auth/instance", "The sign-in page's instance name."),
        new("GET", "api/health", "Liveness probe."),
        new("GET", "api/downloads/{token}", "An agent's curl has no session; the short-lived URL token is the proof, and every token that is not live is 404."),
        new("PUT", "api/uploads/{token}", "An agent's curl has no session; the one-time URL token is the proof, and every token that is not live is 404."),
        new("POST", "api/webhooks/github", "GitHub delivers pull request events with no session; the shared secret's signature is the proof, and the endpoint is 404 while its secret is unset."),
        new("POST", "api/webhooks/gitlab", "GitLab delivers merge request events with no session; the shared secret token is the proof, and the endpoint is 404 while its secret is unset."),
    ];

    public static IReadOnlyList<Entry> SignedIn { get; } =
    [
        new("GET", "api/me", "The caller's own account."),
        new("GET", "api/import/jobs", "Lists only the import jobs of projects the caller administers; a caller who administers none gets an empty list."),
        new("POST", "api/auth/password", "The caller changes their own password."),
        new("POST", "api/auth/sign-out-others", "The caller ends their own other sessions."),
        new("POST", "api/markdown/render", "Touches no project data without workItemId; with one, it checks access first (MarkdownImageRenderTests)."),
        new("GET", "api/users/by-email", "Exact-email lookup for adding members; touches no project data."),
        new("GET", "api/projects", "Lists only the projects the caller is a member of."),
        new("POST", "api/projects", "Not project-scoped: creating a project is gated by the instance's project_creation rule, and a copy needs membership of the source."),
        new("GET", "api/projects/key-check", "Not project-scoped: says whether a key is free, never naming another project."),
        new("GET", "api/projects/first-sprint", "Not project-scoped: the dates of a new project's Sprint 1, computed from today and the working days asked for."),
        new("GET", "api/me/preferences/{key}", "The caller's own preference."),
        new("PUT", "api/me/preferences/{key}", "The caller's own preference."),
        new("POST", "api/me/preferences/reset-views", "The caller's own preferences."),
        new("GET", "api/me/agents", "The caller's own connected agents."),
        new("PUT", "api/me/profile", "The caller's own display name and avatar colour."),
        new("PUT", "api/me/photo", "The caller's own photo."),
        new("DELETE", "api/me/photo", "The caller's own photo."),
        new("GET", "api/users/{id:guid}/photo", "Any signed-in user may read any user's photo: it is what every card and presence stack shows, and it reveals nothing a UserRef does not."),
        new("DELETE", "api/me/agents/{id:guid}", "Revokes one of the caller's own connected agents; another user's id is not_found."),
        new("GET", "api/agents/consent/{id:guid}", "A pending authorize request is the caller's own; another user's is not_found."),
        new("POST", "api/agents/consent/{id:guid}", "Decides the caller's own pending authorize request; another user's is not_found."),
        new("GET", "api/agents/mcp-url", "The server's own /mcp address; names nothing about a project."),
    ];

    // Allow-listed entries with a route parameter, other than the caller-owned and any-user routes below. A parameter usually means an
    // entity id, and an entity route needs a project (or instance) rule: it gets a sweep row, not an allow-list entry.
    public static IReadOnlyList<Entry> WithParameters(IEnumerable<Entry> entries)
        => entries.Where(e => e.Template.Contains('{', StringComparison.Ordinal) && !CallerOwnedRoutes.Contains(e.Template) && !AnyUserRoutes.Contains(e.Template) && !TokenRoutes.Contains(e.Template)).ToList();

    // Routes whose parameter names a row that belongs to the caller (a preference, a connected agent, a pending
    // authorize request): the service answers not_found for anyone else's, and no project is involved.
    private static readonly string[] CallerOwnedRoutes =
    [
        "api/me/preferences/{key}",
        "api/me/agents/{id:guid}",
        "api/agents/consent/{id:guid}",
    ];

    // The parameter names any user, and every signed-in user may read it (v1: one organisation per instance); an unknown id is not_found.
    private static readonly string[] AnyUserRoutes = ["api/users/{id:guid}/photo"];

    // The parameter is a secret token that is itself the credential; an unknown one is not_found.
    private static readonly string[] TokenRoutes = ["api/downloads/{token}", "api/uploads/{token}"];

    public static bool Contains(IReadOnlyList<Entry> list, ApiAction action)
        => list.Any(e => e.Method == action.Method && e.Template == action.Template);
}
