using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Views.InspectionMetrics;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.InspectionMetrics;

// inspection_metrics (K4): the Inspection page's view model for the same query, serialised with InspectionMetricsJson.McpOptions
// (snake_case names and enum values) plus open_url. The view already carries `project` ({id, key, name}), so the envelope's own
// project key is that object and not a second string. It has no logic of its own: IInspectionMetrics checks access and validates the query.
[McpServerToolType]
internal sealed class InspectionMetricsTools(ProjectResolver projects, IInspectionMetrics metrics, IAgentAddresses addresses)
{
    [McpServerTool(Name = "inspection_metrics"), McpScope(McpScopes.Read)]
    [Description("A project's lead time per task, the numbers on the Inspection page, as JSON: the headline (last finished sprint, the running one as so far), one entry per sprint with its mean and a dot per task, quarters, the slowest tasks, the tasks left out for want of a start, escaping bugs, tokens per task from the agent runs as cost (null when agent runs are off; tokens only, never money), and with `period` that sprint's tasks and Features. Lead time runs from a task's first move to Active to its last commit (or its resolve when no commit time is recorded). Times are ISO 8601 UTC, hours are rounded to one decimal and labels are made on the server. No findings: draw your own conclusions. Use it for 'how fast did we deliver last sprint?'.")]
    public async Task<CallToolResult> InspectionMetrics(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("The sprints to show: last6 (default), quarter, lastQuarter or all.")] string? range = null,
        [Description("Cut the sprints by none (default), feature or who.")] string? cut = null,
        [Description("A sprint id from this tool's periods. Adds that sprint's tasks and Features as period_detail.")] string? period = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        var view = await metrics.GetAsync(resolved.Id, new InspectionMetricsQuery(range, cut, period), cancellationToken);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;

        var body = JsonSerializer.SerializeToNode(view, InspectionMetricsJson.McpOptions)!.AsObject();
        var result = new JsonObject
        {
            ["open_url"] = McpOpenUrls.InspectionMetrics(issuer, resolved.Key),
        };
        foreach (var property in body.ToList())
        {
            body.Remove(property.Key);
            result[property.Key] = property.Value;
        }

        return McpToolResult.Of(JsonSerializer.SerializeToElement(result), Summary(resolved.Key, view));
    }

    private static string Summary(string key, InspectionMetricsViewDto view)
        => $"{key} inspection: {view.Periods.Count} sprints";
}
