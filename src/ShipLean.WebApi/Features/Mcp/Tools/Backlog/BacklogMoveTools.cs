using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Backlog;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Status;

namespace ShipLean.WebApi.Features.Mcp.Tools.Backlog;

// Planning moves: items into a sprint or back to the backlog, and an item up or down the backlog. A stale revision is a
// conflict the agent reads and decides on; nothing here retries by itself.
[McpServerToolType]
internal sealed class BacklogMoveTools(
    ProjectResolver projects,
    ItemResolver items,
    SprintResolver sprints,
    IWorkItemStore store,
    BacklogReads reads,
    IAgentAddresses addresses)
{
    private const int MaxItems = 25;

    [McpServerTool(Name = "move_to_sprint"), McpScope(McpScopes.Plan)]
    [Description("Move one or a few work items (up to 25) into a sprint, or back to the backlog with sprint \"backlog\". Their place in the backlog order is kept. Send each item's expected_revision from get_work_item to be told if someone changed it meanwhile; a changed item stops the whole move, nothing is moved, and you get the conflict to decide on.")]
    public async Task<CallToolResult> MoveToSprint(
        [Description("The items to move, 1 to 25.")] IReadOnlyList<MoveItem> items_to_move,
        [Description("current, next, a sprint's name, or backlog to take them out of every sprint.")] string sprint,
        [Description("The project key, such as SHIP. Left out, it is the project of the first item.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        // The project comes from the first item's key when none is named; the item resolver then refuses any other project's key.
        var firstKey = items_to_move?.FirstOrDefault()?.Item?.Trim();
        var projectKey = project ?? (firstKey is { } k && k.Contains('-') ? k[..k.LastIndexOf('-')] : null);
        var resolvedProject = await projects.ResolveAsync(projectKey, forWrite: true, cancellationToken);
        if (items_to_move is null || items_to_move.Count == 0 || items_to_move.Count > MaxItems)
        {
            throw ApiException.Validation($"Pass 1 to {MaxItems} items.", "items_to_move");
        }
        if (items_to_move.Any(i => string.IsNullOrWhiteSpace(i.Item)))
        {
            throw ApiException.Validation("Every item needs a key such as SHIP-123.", "items_to_move");
        }

        var toBacklog = string.Equals(sprint?.Trim(), "backlog", StringComparison.OrdinalIgnoreCase);
        var target = toBacklog ? null : await sprints.ResolveAsync(resolvedProject.Id, sprint ?? string.Empty, cancellationToken: cancellationToken);

        var snapshots = new List<(WorkItemSnapshot Item, int Expected)>();
        foreach (var wanted in items_to_move)
        {
            var resolved = await items.ResolveAsync(wanted.Item!, ProjectRole.Member, resolvedProject.Id, "items_to_move", cancellationToken);
            if (snapshots.Any(s => s.Item.Id == resolved.Id))
            {
                throw ApiException.Validation($"{resolved.Key} is listed twice.", "items_to_move");
            }

            var snapshot = await store.GetAsync(resolved.Id, cancellationToken);
            snapshots.Add((snapshot, wanted.ExpectedRevision ?? snapshot.Revision));
        }

        var changing = snapshots.Where(s => s.Item.SprintId != target?.Id).ToList();
        if (changing.Count > 0)
        {
            var outcome = await store.PatchManyAsync(
                [.. changing.Select(s => (s.Item.Id, new WorkItemPatch(s.Expected, new WorkItemChanges { Sprint = new FieldSet<Guid?>(target?.Id) })))],
                WriteOptions.User with { Bulk = true }, cancellationToken);
            if (outcome.Conflicts is { Count: > 0 } conflicts)
            {
                throw ApiException.Conflict(Describe(conflicts));
            }
        }

        var after = new List<WorkItemSnapshot>();
        foreach (var (item, _) in snapshots)
        {
            after.Add(await store.GetAsync(item.Id, cancellationToken));
        }

        var moved = after.OrderBy(i => i.BacklogOrder, StringComparer.Ordinal)
            .Select(i => new MovedItem(i.Key, i.Title, changing.Any(c => c.Item.Id == i.Id), i.Revision)).ToList();
        var result = new MoveToSprintResult(
            resolvedProject.Key, target is null
                ? McpOpenUrls.Backlog((await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer, resolvedProject.Key, "stories")
                : McpOpenUrls.SprintBacklog((await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer, resolvedProject.Key, target.Id),
            target?.Name, moved);
        var where = target is null ? "the backlog" : target.Name;
        return StatusReply.Of(result, $"{moved.Count(m => m.Changed)} of {moved.Count} moved to {where}: {string.Join(", ", moved.Select(m => m.Key))}.");
    }

    [McpServerTool(Name = "reorder"), McpScope(McpScopes.Plan)]
    [Description("Move a work item above or below another one in the backlog. Give exactly one of above or below. This changes the order only; it is not a change to the item and leaves no revision.")]
    public async Task<CallToolResult> Reorder(
        [Description("The item to move, such as SHIP-123.")] string item,
        [Description("Put the item directly above this item (its key).")] string? above = null,
        [Description("Put the item directly below this item (its key).")] string? below = null,
        [Description("The revision you last saw. Leave out to move whatever is there now.")] int? expected_revision = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        if ((above is null) == (below is null))
        {
            throw ApiException.Validation("Give exactly one of above or below.", "above");
        }

        var anchorKey = (above ?? below)!;
        var anchor = await items.ResolveAsync(anchorKey, ProjectRole.Member, resolved.ProjectId, above is null ? "below" : "above", cancellationToken);
        if (anchor.Id == resolved.Id)
        {
            throw ApiException.Validation($"{resolved.Key} cannot be moved relative to itself.", above is null ? "below" : "above");
        }

        var snapshot = await store.GetAsync(resolved.Id, cancellationToken);
        var before = above is not null ? anchor.Id : await reads.NextAfterAsync(resolved.ProjectId, anchor.Id, resolved.Id, cancellationToken);
        var outcome = await store.ReorderAsync(resolved.Id, expected_revision ?? snapshot.Revision, before, cancellationToken);
        if (outcome.Written is not { } written)
        {
            throw ApiException.Conflict(Describe([outcome.Conflict!]));
        }

        var projectKey = resolved.Key[..resolved.Key.LastIndexOf('-')];
        var position = above is not null ? $"above {anchor.Key}" : $"below {anchor.Key}";
        var result = new ReorderResult(
            projectKey, McpOpenUrls.Backlog((await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer, projectKey, "stories"), resolved.Key, position, written.Revision);
        return StatusReply.Of(result, $"{resolved.Key} is now {position}.");
    }

    private static string Describe(IReadOnlyList<ConflictDto> conflicts)
    {
        var parts = conflicts.Select(conflict =>
        {
            var theirs = conflict.TheirChanges.Select(r =>
            {
                var who = r.ChangedBy?.Name ?? "Someone";
                var via = r.AgentName is { } agent ? $" via {agent}" : string.Empty;
                return $"{who}{via} changed {string.Join(", ", r.Changes.Select(c => c.Field))}";
            });
            return $"{conflict.Current.Key} is at revision {conflict.Current.Revision}" + (conflict.TheirChanges.Count > 0 ? $" ({string.Join("; ", theirs)})" : string.Empty);
        });
        return $"Nothing was changed because {string.Join(" and ", parts)}, not the revision you had. Read it again with get_work_item and decide.";
    }
}
