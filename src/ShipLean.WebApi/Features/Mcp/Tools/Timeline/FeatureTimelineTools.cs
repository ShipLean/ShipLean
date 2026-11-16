using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Timeline;

// feature_timeline: the page's view model, serialised with TimelineJson.McpOptions (snake_case names and enum values, D17).
// It is not an McpResult record: the DTO carries no attributes, so project and open_url are added to the JSON object.
[McpServerToolType]
internal sealed class FeatureTimelineTools(ItemResolver items, IFeatureTimeline timeline, IAgentAddresses addresses)
{
    [McpServerTool(Name = "feature_timeline"), McpScope(McpScopes.Read)]
    [Description("How a Feature was delivered, drawn after the fact from state history: the page's view model as JSON. Key dates, the execution, one bar per task with its start, end, state periods and review events, the dependency edges, the critical path once the Feature is Done, and the tasks never started. Times are ISO 8601 UTC, positions are hour offsets from scale.start, labels are made on the server. No findings: draw your own conclusions. Use it for 'how did the Payments feature go?'.")]
    public async Task<CallToolResult> FeatureTimeline(
        [Description("The key of the Feature, such as SHIP-12. A key that is not a Feature is not_found.")] string key,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(key, ProjectRole.Member, field: "key", cancellationToken: cancellationToken);
        var view = await timeline.GetAsync(resolved.ProjectId, resolved.Key, cancellationToken);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        var projectKey = resolved.Key[..resolved.Key.IndexOf('-')];

        var body = JsonSerializer.SerializeToNode(view, TimelineJson.McpOptions)!.AsObject();
        var result = new JsonObject
        {
            ["project"] = projectKey,
            ["open_url"] = McpOpenUrls.WorkItem(issuer, projectKey, resolved.Key),
        };
        foreach (var property in body.ToList())
        {
            body.Remove(property.Key);
            result[property.Key] = property.Value;
        }

        return McpToolResult.Of(JsonSerializer.SerializeToElement(result), Summary(resolved.Key, view));
    }

    private static string Summary(string key, FeatureTimelineViewDto view)
    {
        if (view.Bars.Count == 0)
        {
            return $"{key} timeline: not started";
        }

        var days = view.Execution.CalendarDays is { } d ? Math.Round(d, 1) : 0;
        return $"{key} timeline: {view.Bars.Count} tasks over {days.ToString("0.#", System.Globalization.CultureInfo.InvariantCulture)} days, {(view.Finished ? "finished" : "running")}";
    }
}
