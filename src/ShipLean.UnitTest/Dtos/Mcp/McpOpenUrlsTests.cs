using System.Text.RegularExpressions;
using ShipLean.Contracts.Mcp;
using ShipLean.TestSupport;

namespace ShipLean.UnitTest.Dtos.Mcp;

// McpOpenUrls builds paths in C#, so each one is checked against the pattern routes.ts declares for the same screen.
[TestFixture]
public class McpOpenUrlsTests
{
    private const string Base = "https://planner.example.com/";
    private static readonly Guid Sprint = Guid.Parse("00000004-0000-4000-8001-000000000019");

    [Test]
    public void McpOpenUrls_ShouldMatchRoutesTs()
    {
        var patterns = RoutePatterns();
        Dictionary<string, string> built = new()
        {
            ["workItem"] = McpOpenUrls.WorkItem(Base, "SHIP", "SHIP-141"),
            ["storiesBoard"] = McpOpenUrls.Board(Base, "SHIP", "stories"),
            ["featuresBoard"] = McpOpenUrls.Board(Base, "SHIP", "features"),
            ["epicsBoard"] = McpOpenUrls.Board(Base, "SHIP", "epics"),
            ["storiesBacklog"] = McpOpenUrls.Backlog(Base, "SHIP", "stories"),
            ["featuresBacklog"] = McpOpenUrls.Backlog(Base, "SHIP", "features"),
            ["epicsBacklog"] = McpOpenUrls.Backlog(Base, "SHIP", "epics"),
            ["sprints"] = McpOpenUrls.Sprints(Base, "SHIP"),
            ["sprintBacklog"] = McpOpenUrls.SprintBacklog(Base, "SHIP", Sprint),
            ["taskboard"] = McpOpenUrls.Taskboard(Base, "SHIP", Sprint),
            ["workItemAttachment"] = McpOpenUrls.WorkItemAttachment(Base, "SHIP", "SHIP-141", Guid.Parse("00000004-0000-4000-8001-000000000020")),
        };
        var values = new Dictionary<string, string>
        {
            [":key"] = "SHIP",
            [":itemKey"] = "SHIP-141",
            [":sprintId"] = Sprint.ToString(),
            [":attachmentId"] = "00000004-0000-4000-8001-000000000020",
        };

        foreach (var (routeId, url) in built)
        {
            var expected = values.Aggregate(patterns[routeId], (path, kv) => path.Replace(kv.Key, kv.Value, StringComparison.Ordinal));
            url.ShouldBe("https://planner.example.com" + expected);
        }
    }

    [Test]
    public void Planning_ShouldBeTheStoriesBacklogInPlanningMode()
    {
        var patterns = RoutePatterns();

        McpOpenUrls.Planning(Base, "SHIP").ShouldBe("https://planner.example.com" + patterns["storiesBacklog"].Replace(":key", "SHIP", StringComparison.Ordinal) + "?mode=planning");
        RoutesTs().ShouldContain("export const backlogModes = ['list', 'planning', 'tree']");
    }

    [Test]
    public void Backlog_WhenKeyHasUnsafeCharacters_ShouldEscapeIt()
    {
        McpOpenUrls.WorkItem(Base, "A B", "A B-1").ShouldBe("https://planner.example.com/p/A%20B/items/A%20B-1");
    }

    private static string RoutesTs() => File.ReadAllText(Path.Combine(RepoPaths.Root, "src", "ShipLean.Client", "src", "routes.ts"));

    // routeId -> pattern, from the routeDefs table: `  storiesBacklog: {\n    pattern: '/p/:key/backlogs/stories',`.
    private static Dictionary<string, string> RoutePatterns()
        => Regex.Matches(RoutesTs(), @"^\s{2}(\w+): \{\s*pattern: '([^']+)'", RegexOptions.Multiline)
            .ToDictionary(m => m.Groups[1].Value, m => m.Groups[2].Value);
}
