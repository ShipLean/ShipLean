using System.Text.Json.Nodes;

namespace ShipLean.TestSupport.Jira;

// The raw responses of the sample project, read from Jira/platform/ at run time (the TestSupport project embeds nothing).
public static class JiraStubFiles
{
    public const string ProjectKey = "PLATFORM";

    public const string AttachmentIssueKey = "PLATFORM-406";

    public const string AttachmentId = "10601";

    public const string AttachmentFilename = "load-test-dashboard.png";

    public const string CommentedIssueKey = "PLATFORM-404";

    public static string Directory { get; } = Path.Combine(RepoPaths.Root, "src", "ShipLean.TestSupport", "Jira", "platform");

    // PLATFORM-403 .. PLATFORM-412, in key order.
    public static IReadOnlyList<string> IssueKeys { get; } = [.. Enumerable.Range(403, 10).Select(n => $"{ProjectKey}-{n}")];

    public static byte[] AttachmentBytes => ReadBytes("attachments", AttachmentId + ".png");

    public static string ReadText(params string[] path) => File.ReadAllText(PathOf(path));

    public static byte[] ReadBytes(params string[] path) => File.ReadAllBytes(PathOf(path));

    public static bool Exists(params string[] path) => File.Exists(PathOf(path));

    public static JsonNode ReadJson(string baseUrl, string apiVersion, params string[] path)
        => JsonNode.Parse(ReadText(path).Replace("{{base}}", baseUrl, StringComparison.Ordinal).Replace("{{v}}", apiVersion, StringComparison.Ordinal))!;

    private static string PathOf(string[] path) => Path.Combine([Directory, .. path]);
}
