namespace ShipLean.AcceptanceTests.Harness;

public static partial class Links
{
    // Ways of working, with one prompt open when given (its camelCase key, "planReviewer"): /p/:key/ways-of-working?prompt=.
    public static string WaysOfWorking(string projectKey, string? prompt = null) =>
        $"/p/{Segment(projectKey)}/ways-of-working" + (prompt is null ? "" : $"?prompt={Uri.EscapeDataString(prompt)}");
}
