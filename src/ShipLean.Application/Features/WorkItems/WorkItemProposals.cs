using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.WorkItems;

// The after-image a write proposes to the rules: a stored item's fields with a WorkItemChanges applied on top.
internal static class WorkItemProposals
{
    public static ProposedChange From(WorkItemSnapshot item) => new(
        item.Id, item.ProjectId, item.Type, item.Title, item.Description, item.AcceptanceCriteria, item.ReproSteps, item.Severity,
        item.StateId, item.AssigneeId, item.SprintId, item.Priority, item.Points, item.ParentId, item.Tags,
        item.ColumnId, item.ColumnDone, item.LaneId, item.ColumnOrder, item.BacklogOrder,
        item.Goal, item.StatusLine, item.StatusVerifiedAt);

    // A client may send null for a list (an explicit "addLinks": null): that is an empty list.
    public static WorkItemChanges? Clean(WorkItemChanges? changes)
        => changes is null ? null : changes with { AddLinks = changes.AddLinks ?? [], RemoveLinkIds = changes.RemoveLinkIds ?? [] };

    // at is the moment this write stamps: writing the status line (even unchanged) or confirming it verifies the status then.
    public static ProposedChange Apply(ProposedChange change, WorkItemChanges? changes, DateTimeOffset at)
    {
        if (changes is null)
        {
            return change;
        }

        return change with
        {
            Title = changes.Title is { } title ? title.Value : change.Title,
            Description = changes.Description is { } description ? description.Value ?? "" : change.Description,
            AcceptanceCriteria = changes.AcceptanceCriteria is { } criteria ? criteria.Value ?? [] : change.AcceptanceCriteria,
            ReproSteps = changes.ReproSteps is { } repro ? repro.Value ?? "" : change.ReproSteps,
            Severity = changes.Severity is { } severity ? severity.Value : change.Severity,
            StateId = changes.State is { } state ? state.Value : change.StateId,
            AssigneeId = changes.Assignee is { } assignee ? assignee.Value : change.AssigneeId,
            SprintId = changes.Sprint is { } sprint ? sprint.Value : change.SprintId,
            Priority = changes.Priority is { } priority ? priority.Value : change.Priority,
            Points = changes.Points is { } points ? WorkItemPoints.Normalise(points.Value) : change.Points,
            ParentId = changes.Parent is { } parent ? parent.Value : change.ParentId,
            Tags = changes.Tags is { } tags ? tags.Value ?? [] : change.Tags,
            Goal = changes.Goal is { } goal ? goal.Value ?? "" : change.Goal,
            StatusLine = changes.StatusLine is { } statusLine ? statusLine.Value ?? "" : change.StatusLine,
            StatusVerifiedAt = changes.StatusLine is not null || changes.ConfirmStatus ? at : change.StatusVerifiedAt,
        };
    }

    public static WorkItemFieldView ViewOf(ProposedChange p, IReadOnlyList<LinkSnapshot> links) => new(
        p.Title, p.Type, p.StateId, p.AssigneeId, p.Priority, p.Points, p.Tags, p.SprintId, p.ParentId, p.Severity,
        WorkItemFieldView.NullIfEmpty(p.Description), p.AcceptanceCriteria, WorkItemFieldView.NullIfEmpty(p.ReproSteps), [.. links.Select(l => l.Value)],
        WorkItemFieldView.NullIfEmpty(p.Goal), WorkItemFieldView.NullIfEmpty(p.StatusLine), WorkItemFieldView.Instant(p.StatusVerifiedAt));
}
