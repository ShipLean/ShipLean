using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Orient;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.Core;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Feature;

namespace ShipLean.WebApi.Features.Mcp.Tools.Orient;

[McpServerToolType]
internal sealed class GetWorkItemTool(
    ItemResolver items,
    IWorkItemStore store,
    ICommentStore comments,
    IAttachmentStore attachments,
    IWorkTreeReads tree,
    OrientReads reads,
    IAgentAddresses addresses,
    TimeProvider clock)
{
    private const int CommentCount = 5;
    private const int HistoryCount = 10;
    private const int ChangeTextLength = 200;

    [McpServerTool(Name = "get_work_item"), McpScope(McpScopes.Read)]
    [Description("Everything about one work item by key, like SHIP-123: its fields, revision, the parents above it, its children with their states, links, its attachments, the last 5 comments (or the 5 before comments_before) and the last 10 history entries (with the agent's name when an agent made the change). Keep the revision if you mean to change the item.")]
    public async Task<CallToolResult> GetWorkItemAsync(
        [Description("The work item key, like SHIP-123.")] string key,
        [Description("ISO 8601 time of the oldest comment you already have; returns the 5 comments before it.")] string? comments_before = null,
        CancellationToken cancellationToken = default)
    {
        var item = await items.ResolveAsync(key, ProjectRole.Member, field: "key", cancellationToken: cancellationToken);
        var snapshot = await store.GetAsync(item.Id, cancellationToken);
        var revisions = (await store.GetRevisionsAsync(item.Id, cancellationToken)).OrderByDescending(r => r.Rev).Take(HistoryCount).ToList();
        var (page, commentsOlder) = CommentPage(await comments.ListAsync(item.Id, cancellationToken), comments_before);
        var listed = await attachments.ListAsync(item.Id, cancellationToken);
        var children = (await tree.DescendantsAsync(item.ProjectId, item.Id, null, cancellationToken)).Where(c => c.Depth == 1).ToList();
        var isFeature = snapshot.Type == WorkItemTypeId.Feature;
        var names = await reads.NamesAsync(
            item.Id,
            [.. revisions.Where(r => r.ChangedBy is not null).Select(r => r.ChangedBy!.Value).Concat(isFeature && snapshot.StatusVerifiedBy is { } verifier ? [verifier] : []).Distinct()],
            cancellationToken);
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;

        var result = new WorkItemResult(
            names.ProjectKey,
            McpOpenUrls.WorkItem(issuer, names.ProjectKey, snapshot.Key),
            snapshot.Key,
            snapshot.Type.ToString(),
            snapshot.Title,
            names.StateName,
            names.AssigneeName,
            names.SprintName,
            snapshot.Priority,
            snapshot.Points,
            snapshot.Severity,
            snapshot.Tags,
            snapshot.Description,
            [.. snapshot.AcceptanceCriteria.Select(c => new WorkItemResult.Criterion(c.Text, c.Done))],
            snapshot.ReproSteps,
            snapshot.Revision,
            [.. names.Parents.Select(p => new WorkItemResult.Ref(p.Key, p.Type, p.Title))],
            [.. children.Select(c => new WorkItemResult.Child(c.Key, c.Type.Name, c.Title, c.State.Name, c.Assignee?.Name))],
            [.. snapshot.Links.Select(l => new WorkItemResult.Link(CamelCase(l.Value.Type), l.Value.Key, l.Value.Title, l.Value.Url))],
            [.. page.Select(c => new WorkItemResult.Comment(c.Author?.Name, c.AgentName, c.CreatedAt.ToString("O"), c.Body))],
            commentsOlder,
            [.. listed.Select(a => new WorkItemResult.Attachment(
                a.Name, a.SizeBytes, a.ContentType, a.Kind == AttachmentKind.Markdown, a.UploadedBy?.Name, a.CreatedAt.ToString("O"), a.AgentName, a.Via))],
            [.. revisions.Select(r => new WorkItemResult.HistoryEntry(
                r.Rev,
                r.ChangedBy is { } who && names.Users.TryGetValue(who, out var name) ? name : null,
                r.AgentName,
                r.ChangedAt.ToString("O"),
                [.. r.Changes.Select(c => new WorkItemResult.Change(c.Field, Shown(c.FromLabel, c.From), Shown(c.ToLabel, c.To)))]))],
            isFeature ? snapshot.Goal : null,
            isFeature ? snapshot.StatusLine : null,
            isFeature ? VerifiedLine.Of(snapshot, VerifierName(snapshot, names), clock) : null,
            isFeature ? snapshot.Slug : null,
            StartedLabel(snapshot));
        return OrientReply.Of(result, $"{result.Key} {result.Title}: {result.State}, revision {result.Revision}, {result.Children.Count} children, {result.Attachments.Count} attachments, {result.Comments.Count} recent comments.");
    }

    private static (IReadOnlyList<CommentDto> Page, string? Older) CommentPage(IReadOnlyList<CommentDto> all, string? before)
    {
        var ordered = all.OrderByDescending(c => c.CreatedAt).ToList();
        if (!string.IsNullOrWhiteSpace(before))
        {
            if (!DateTimeOffset.TryParse(before, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var cursor))
            {
                throw ApiException.Validation("comments_before must be an ISO 8601 time, such as 2026-10-20T09:00:00Z.", "comments_before");
            }
            ordered = ordered.Where(c => c.CreatedAt < cursor).ToList();
        }
        var page = ordered.Take(CommentCount).ToList();
        if (page.Count == 0)
        {
            return (page, null);
        }
        var oldest = page[^1].CreatedAt;
        var older = ordered.Count > CommentCount ? oldest.ToString("O") : null;
        return (page, older);
    }

    private static string? VerifierName(WorkItemSnapshot snapshot, ItemNames names)
        => snapshot.StatusVerifiedBy is { } by && names.Users.TryGetValue(by, out var name) ? name : null;

    private string? StartedLabel(WorkItemSnapshot snapshot)
        => snapshot is { Type: WorkItemTypeId.Feature, StartedOn: { } started }
            ? DisplayDate.ShortDay(DisplayDate.StartOfDay(started, clock.LocalTimeZone), clock.GetUtcNow(), clock.LocalTimeZone)
            : null;

    private static string CamelCase(LinkType type) => char.ToLowerInvariant(type.ToString()[0]) + type.ToString()[1..];

    private static string? Text(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.Null or JsonValueKind.Undefined => null,
        JsonValueKind.String => value.GetString(),
        _ => value.ToString(),
    };

    // The label the person saw, else the stored value as text; long text is cut so a rewritten description stays readable.
    private static string? Shown(string? label, JsonElement value)
    {
        var text = label ?? Text(value);
        return text is { Length: > ChangeTextLength } ? text[..ChangeTextLength] + "…" : text;
    }
}
