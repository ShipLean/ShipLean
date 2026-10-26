using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Notes;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Writes;

namespace ShipLean.WebApi.Features.Mcp.Tools.Capture;

[McpServerToolType]
internal sealed class RefineTools(ItemResolver itemResolver, IWorkItemStore store, NoteWriter notes, StaleWrites writes, CaptureSupport support)
{
    [McpServerTool(Name = "refine_work_item"), McpScope(McpScopes.Write)]
    [Description("Improve one work item: rewrite its title, description or acceptance criteria, and set its points, tags, priority, severity or parent. A rewritten checklist keeps the ticked state of every line whose text is unchanged. Pass expected_revision (from get_work_item) to refuse if someone has edited it since you read it. A note becomes a comment explaining the change.")]
    public async Task<CallToolResult> RefineWorkItem(
        [Description("The item's key, such as SHIP-12.")] string item,
        [Description("A new title.")] string? title = null,
        [Description("A new description in Markdown; replaces the old one.")] string? description = null,
        [Description("The whole acceptance checklist, one line each; replaces the old one.")] IReadOnlyList<string>? acceptance_criteria = null,
        [Description("Story points.")] decimal? points = null,
        [Description("The whole tag list; replaces the old one.")] IReadOnlyList<string>? tags = null,
        [Description("1 (highest) to 4.")] int? priority = null,
        [Description("A bug's severity, 1 (critical) to 4 (low).")] int? severity = null,
        [Description("The parent's key, or none to detach it.")] string? parent = null,
        [Description("The revision you read. A stale write is refused as a conflict.")] int? expected_revision = null,
        [Description("Why you changed it. Added as a comment after the change.")] string? note = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await itemResolver.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var snapshot = await store.GetAsync(resolved.Id, cancellationToken);

        Guid? parentId = null;
        var detach = !string.IsNullOrWhiteSpace(parent) && NameMatcher.Same(parent, "none");
        if (!string.IsNullOrWhiteSpace(parent) && !detach)
        {
            parentId = (await itemResolver.ResolveAsync(parent, ProjectRole.Member, resolved.ProjectId, "parent", cancellationToken)).Id;
        }

        var parentChange = detach ? new FieldSet<Guid?>(null) : null;
        if (parentId is not null)
        {
            parentChange = new FieldSet<Guid?>(parentId);
        }

        var changes = new WorkItemChanges
        {
            Title = title is null ? null : new FieldSet<string>(title),
            Description = description is null ? null : new FieldSet<string>(description),
            AcceptanceCriteria = acceptance_criteria is null ? null : new FieldSet<IReadOnlyList<ChecklistItem>>(Rewrite(snapshot.AcceptanceCriteria, acceptance_criteria)),
            Points = points is null ? null : new FieldSet<decimal?>(points),
            Tags = tags is null ? null : new FieldSet<IReadOnlyList<string>>(tags),
            Priority = priority is null ? null : new FieldSet<int>(priority.Value),
            Severity = severity is null ? null : new FieldSet<int?>(severity),
            Parent = parentChange,
        };
        var fields = StaleWrites.Fields(changes);
        if (fields.Count == 0)
        {
            throw ApiException.Validation("Say what to change: a title, description, acceptance criteria, points, tags, priority, severity or parent.");
        }

        var written = await writes.PatchAsync(resolved.Id, resolved.Key, changes, expected_revision ?? snapshot.Revision, expected_revision is not null, cancellationToken);

        var noteOutcome = string.IsNullOrWhiteSpace(note) ? null : await notes.AddAsync(resolved.Id, note, cancellationToken);

        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        var result = new RefineResult(
            project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), resolved.Key,
            written.Revision, [.. written.Changes.Select(c => c.Field)], written.Notices, noteOutcome?.Added, noteOutcome?.Error);
        var summary = written.Changes.Count == 0 ? $"{resolved.Key} already had those values; nothing changed." : $"Updated {resolved.Key}: {string.Join(", ", result.Changed)}.";
        return CaptureSupport.Reply(result, noteOutcome is { Added: false } ? summary + " The note was not added: " + noteOutcome.Error : summary);
    }

    // The new lines, each keeping the Done flag of an old line with the same text (one old line answers one new line).
    private static ChecklistItem[] Rewrite(IReadOnlyList<ChecklistItem> old, IReadOnlyList<string> lines)
    {
        var remaining = old.ToList();
        return [.. lines.Select(line =>
        {
            var text = line.Trim();
            var match = remaining.FindIndex(o => o.Text == text);
            var done = match >= 0 && remaining[match].Done;
            if (match >= 0)
            {
                remaining.RemoveAt(match);
            }
            return new ChecklistItem(text, done);
        })];
    }
}
