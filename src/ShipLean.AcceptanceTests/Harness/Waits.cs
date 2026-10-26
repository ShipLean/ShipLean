namespace ShipLean.AcceptanceTests.Harness;

// Waiting helpers. Tests wait for a locator or a network response, never for time.
public static class Waits
{
    // Runs the action (a click, a navigation) and returns the app's API response to the matching request.
    // Example: page.RunAndWaitForApiAsync("/api/projects", "GET", () => page.GotoAsync("/")).
    // Matches the request path exactly, ignoring the query string.
    public static Task<IResponse> RunAndWaitForApiAsync(this IPage page, string path, string method, Func<Task> action) =>
        page.RunAndWaitForResponseAsync(
            action,
            r => string.Equals(r.Request.Method, method, StringComparison.OrdinalIgnoreCase)
                && new Uri(r.Url).AbsolutePath == path);
}
