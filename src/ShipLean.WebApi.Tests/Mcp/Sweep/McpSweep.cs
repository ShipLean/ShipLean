using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport.AccessSweep.Mcp;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Sweep;

// The MCP access sweep: every registered tool has a row, no row names a tool that is not registered, and each row's call,
// made as Joel, answers not_found. Rows name an entity in a project Joel is not a member of (the seeded PART), so the only
// thing that can refuse the call is the project access rule.
public static class McpSweep
{
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

    public static async Task<IReadOnlyList<string>> CallProblemsAsync(Func<string, string, Task<McpReply>> call, IEnumerable<McpSweepRow> rows)
    {
        var problems = new List<string>();
        foreach (var row in rows)
        {
            var reply = await call(row.Tool, row.Arguments ?? "{}");
            var code = reply.IsToolError() ? reply.Structured().GetProperty("error").GetProperty("code").GetString() : "ok";
            if (code != ApiErrorCodes.NotFound)
            {
                problems.Add($"{row.Tool} answered {code} to Joel; expected {ApiErrorCodes.NotFound}");
            }
        }
        return problems;
    }
}
