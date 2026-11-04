using System.Reflection;
using ModelContextProtocol.Server;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.UnitTest.Mcp;

// K2: a new MCP tool must be classified here, and a tool that lists items must be in the BAU sweep, so a new list surface cannot leak a bau item unseen.
[TestFixture]
public class BauToolCoverageTests
{
    private const string SweepPath = "src/ShipLean.WebApi.Tests/WorkItems/Bau/BauHiddenSweepTests.cs";

    private enum Kind
    {
        ListsItems,
        ByKeyOnly,
        NoItems,
    }

    private static readonly Dictionary<string, Kind> Classified = new()
    {
        ["search"] = Kind.ListsItems,
        ["whats_changed_since"] = Kind.ListsItems,
        ["find_work_to_pick_up"] = Kind.ListsItems,
        ["review_backlog"] = Kind.ListsItems,
        ["plan_sprint"] = Kind.ListsItems,
        ["end_sprint"] = Kind.ListsItems,
        ["sprint_status"] = Kind.ListsItems,
        ["sprint_review"] = Kind.ListsItems,
        ["feature_progress"] = Kind.ListsItems,
        ["get_work_item"] = Kind.ByKeyOnly,
        ["read_attachment"] = Kind.ByKeyOnly,
        ["get_brief"] = Kind.ByKeyOnly,
        ["plan_status"] = Kind.ByKeyOnly,
        ["refine_work_item"] = Kind.ByKeyOnly,
        ["comment"] = Kind.ByKeyOnly,
        ["link"] = Kind.ByKeyOnly,
        ["set_status"] = Kind.ByKeyOnly,
        ["pick_up"] = Kind.ByKeyOnly,
        ["update_progress"] = Kind.ByKeyOnly,
        ["hand_back"] = Kind.ByKeyOnly,
        ["report_task"] = Kind.ByKeyOnly,
        ["report_link_state"] = Kind.ByKeyOnly,
        ["attach_markdown"] = Kind.ByKeyOnly,
        ["attach_file"] = Kind.ByKeyOnly,
        ["break_down"] = Kind.ByKeyOnly,
        ["move_to_sprint"] = Kind.ByKeyOnly,
        ["reorder"] = Kind.ByKeyOnly,
        ["capture_work"] = Kind.ByKeyOnly,
        ["capture_plan"] = Kind.ByKeyOnly,
        ["create_feature"] = Kind.ByKeyOnly,
        ["decide_retro_actions"] = Kind.ByKeyOnly,
        ["create_sprint"] = Kind.NoItems,
        ["create_sprint_demo"] = Kind.NoItems,
        ["list_projects"] = Kind.NoItems,
        ["planning_guide"] = Kind.NoItems,
        ["list_retrospectives"] = Kind.NoItems,
        ["log_retrospective"] = Kind.NoItems,
    };

    private static IReadOnlyList<string> ToolNames()
        => [.. typeof(McpScopeAttribute).Assembly.GetTypes()
            .SelectMany(type => type.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            .Select(method => method.GetCustomAttribute<McpServerToolAttribute>()?.Name)
            .OfType<string>()
            .Order(StringComparer.Ordinal)];

    [Test]
    public void EveryMcpTool_ShouldBeClassified()
    {
        var names = ToolNames();

        names.Where(name => !Classified.ContainsKey(name)).ShouldBeEmpty("Classify these tools in BauToolCoverageTests (ListsItems tools also go in BauHiddenSweepTests)");
        Classified.Keys.Where(name => !names.Contains(name)).ShouldBeEmpty("These classified tools no longer exist");
        names.Count.ShouldBe(Classified.Count);
    }

    [Test]
    public void EveryToolThatListsItems_ShouldBeCalledInTheSweep()
    {
        var sweep = File.ReadAllText(Path.Combine(RepoRoot(), SweepPath));

        Classified.Where(pair => pair.Value == Kind.ListsItems).Select(pair => pair.Key)
            .Where(name => !sweep.Contains($"\"{name}\"", StringComparison.Ordinal))
            .ShouldBeEmpty("These tools list items but BauHiddenSweepTests never calls them");
    }

    private static string RepoRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, SweepPath)))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new FileNotFoundException(SweepPath);
    }
}
