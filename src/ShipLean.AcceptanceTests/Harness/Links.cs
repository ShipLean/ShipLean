namespace ShipLean.AcceptanceTests.Harness;

// App URLs built from strings. The client's TypeScript link builders cannot be called from C#, so each feature adds
// its own partial file (Links.<Area>.cs) with its routes, built from these helpers, and keeps it in step with the
// client's routes. Pass the result to Page.GotoAsync (the browser context has the app as its base URL).
public static partial class Links
{
    public static string Home() => "/";

    // Absolute URL for a path, given a host's BaseUrl.
    public static string Absolute(string baseUrl, string path) => baseUrl.TrimEnd('/') + path;

    // Deep-link hash: Hash(("item", "5"), ("tab", "a b")) -> "#item=5&tab=a%20b". A null value makes a bare key.
    public static string Hash(params (string Key, string? Value)[] parameters) => "#" + Pairs(parameters);

    // Query string with the same rules: Query(("q", "x y")) -> "?q=x%20y".
    public static string Query(params (string Key, string? Value)[] parameters) => "?" + Pairs(parameters);

    // One escaped path segment.
    public static string Segment(string value) => Uri.EscapeDataString(value);

    private static string Pairs((string Key, string? Value)[] parameters) =>
        string.Join("&", parameters.Select(p => p.Value is null ? Segment(p.Key) : $"{Segment(p.Key)}={Segment(p.Value)}"));
}
