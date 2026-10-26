namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // A project's settings tab ("general", "states", "members", "sprints", "integrations"): /p/:key/settings/:tab.
    public static string ProjectSettings(string projectKey, string tab) =>
        $"/p/{Segment(projectKey)}/settings/{Segment(tab)}";

    // The instance settings tab ("general", "users", "projects"): /instance/:tab.
    public static string InstanceSettings(string tab) => $"/instance/{Segment(tab)}";

    // The New project page: /projects/new.
    public static string NewProject() => "/projects/new";

    // Your profile: /profile.
    public static string Profile() => "/profile";
}
