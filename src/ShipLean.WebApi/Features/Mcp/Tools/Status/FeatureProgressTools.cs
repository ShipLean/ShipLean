using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Status;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Features.Mcp.Tools.Status;

[McpServerToolType]
internal sealed class FeatureProgressTools(
    ItemResolver items,
    FeatureProgressReads reads,
    IWorkTreeReads tree,
    IAgentAddresses addresses)
{
    private const int MaxOpenChildren = 100;

    [McpServerTool(Name = "feature_progress"), McpScope(McpScopes.Read)]
    [Description("How a feature or epic is going: how many stories, bugs, tasks and nested features under it are open and closed, the points done against the total, and the open ones with their state and the sprint they are in. The same numbers the Features board shows. Use it for 'how is the Payments epic going?'.")]
    public async Task<CallToolResult> FeatureProgress(
        [Description("The key of the epic or feature, such as SHIP-12.")] string item,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var head = await reads.HeadAsync(resolved.Id, cancellationToken);
        var rollups = await tree.RollupsAsync(resolved.ProjectId, [resolved.Id], null, cancellationToken);
        var descendants = await tree.DescendantsAsync(resolved.ProjectId, resolved.Id, null, cancellationToken);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;

        var byType = rollups
            .Select(r => new FeatureProgressResult.TypeProgress(Word(r.Type), r.Open, r.Closed, Done(r), r.TotalPoints, r.Unestimated))
            .ToList();
        var pointed = rollups.Where(r => r.Type is WorkItemTypeId.Story or WorkItemTypeId.Bug && r.TotalPoints is not null).ToList();
        var open = descendants.Where(d => d.State.Category != StateCategory.Completed).ToList();
        var openChildren = open
            .Take(MaxOpenChildren)
            .Select(d => new FeatureProgressResult.OpenChild(d.Key, d.Title, Word(d.Type.Id), d.State.Name, d.Sprint?.Name, d.Assignee?.Name, d.Points))
            .ToList();

        var result = new FeatureProgressResult(
            head.ProjectKey, McpOpenUrls.WorkItem(issuer, head.ProjectKey, resolved.Key),
            resolved.Key, head.Title, head.Type, head.State, byType,
            pointed.Count > 0 ? pointed.Sum(r => Done(r) ?? 0) : null, pointed.Count > 0 ? pointed.Sum(r => r.TotalPoints ?? 0) : null,
            open.Count, openChildren);
        var closed = rollups.Sum(r => r.Closed);
        return StatusReply.Of(result, $"{resolved.Key} {head.Title}: {closed} of {closed + rollups.Sum(r => r.Open)} items under it are done, {open.Count} open.");
    }

    private static decimal? Done(WorkTreeRollupDto rollup) => rollup.TotalPoints - (rollup.OpenPoints ?? 0);

    private static string Word(WorkItemTypeId type) => type.ToString().ToLowerInvariant();
}
