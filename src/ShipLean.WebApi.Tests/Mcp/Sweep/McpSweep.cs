using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Sweep;

// The MCP access sweep: every registered tool has a row, no row names a tool that is not registered, and each row's call
// (but a NoProject row's, which names no project to refuse) made as Joel answers not_found. Rows name an entity in a project Joel is not a member of (the seeded PART), so the only
// thing that can refuse the call is the project access rule. A NoProject row is only skipped when its tool is in ProjectFreeTools, it gives a reason and its
// tool's input schema (from tools/list) has no project argument; otherwise it is a problem, so a project-scoped tool can't opt out.
public static class McpSweep
{
    // The tools reviewed as naming no project at all, so the sweep has no project to refuse them. A tool addressed by a key or an
    // id (get_work_item) is project-scoped without a project argument, so the schema check alone can't tell; only this set can.
    // Adding a name needs a reason in review. list_projects and planning_guide are not here: their rows are called and answer not_found.
    public static readonly IReadOnlySet<string> ProjectFreeTools = new HashSet<string>(StringComparer.Ordinal)
    {
        // The payload is bound to the caller, their agent authorization and the tool; the tool that uses it checks project access.
        "create_payload_upload",
    };

    // Every IMcpSweepRows in this test assembly, one file per tool area.
    public static IReadOnlyList<McpSweepRow> AllRows()
        => typeof(McpSweep).Assembly.GetTypes()
            .Where(type => typeof(IMcpSweepRows).IsAssignableFrom(type) && type is { IsClass: true, IsAbstract: false })
            .SelectMany(type => ((IMcpSweepRows)Activator.CreateInstance(type)!).Rows)
            .ToList();

    public static IReadOnlyList<string> ToolNames(IServiceProvider services)
        => services.GetRequiredService<IOptions<McpServerOptions>>().Value.ToolCollection?.Select(tool => tool.ProtocolTool.Name).Order(StringComparer.Ordinal).ToList() ?? [];

    public static IReadOnlyList<string> RowProblems(IEnumerable<string> tools, IEnumerable<McpSweepRow> rows)
    {
        var toolList = tools.ToList();
        var rowList = rows.ToList();
        return toolList.Where(tool => rowList.All(row => row.Tool != tool)).Select(tool => $"tool {tool} has no sweep row")
            .Concat(rowList.Where(row => !toolList.Contains(row.Tool)).Select(row => $"sweep row names {row.Tool}, which is not a registered tool"))
            .ToList();
    }

    // Each tool's input schema by name, from a tools/list reply.
    public static IReadOnlyDictionary<string, JsonElement> InputSchemas(McpReply toolsList)
        => toolsList.Json().GetProperty("result").GetProperty("tools").EnumerateArray()
            .ToDictionary(tool => tool.GetProperty("name").GetString()!, tool => tool.GetProperty("inputSchema").Clone(), StringComparer.Ordinal);

    public static async Task<IReadOnlyList<string>> CallProblemsAsync(
        Func<string, string, Task<McpReply>> call, IReadOnlyDictionary<string, JsonElement> inputSchemas, IEnumerable<McpSweepRow> rows)
    {
        var problems = new List<string>();
        foreach (var row in rows)
        {
            if (row.NoProject is not null)
            {
                problems.AddRange(NoProjectProblems(row, inputSchemas));
                continue;
            }

            var reply = await call(row.Tool, row.Arguments ?? "{}");
            var code = reply.IsToolError() ? reply.Structured().GetProperty("error").GetProperty("code").GetString() : "ok";
            if (code != ApiErrorCodes.NotFound)
            {
                problems.Add($"{row.Tool} answered {code} to Joel; expected {ApiErrorCodes.NotFound}");
            }
        }
        return problems;
    }

    // The set check comes last and only speaks when nothing else is wrong with the row, so each row reports its first cause.
    private static IEnumerable<string> NoProjectProblems(McpSweepRow row, IReadOnlyDictionary<string, JsonElement> inputSchemas)
    {
        var problems = new List<string>();
        if (string.IsNullOrWhiteSpace(row.NoProject))
        {
            problems.Add($"{row.Tool} is a NoProject row with a blank reason");
        }
        if (!inputSchemas.TryGetValue(row.Tool, out var schema))
        {
            problems.Add($"{row.Tool} is a NoProject row but is not in tools/list");
        }
        else if (schema.TryGetProperty("properties", out var properties) && properties.TryGetProperty("project", out _))
        {
            problems.Add($"{row.Tool} takes a project argument, so it can't be a NoProject row");
        }
        if (problems.Count == 0 && !ProjectFreeTools.Contains(row.Tool))
        {
            problems.Add($"{row.Tool} is a NoProject row but is not in McpSweep.ProjectFreeTools");
        }
        return problems;
    }
}
