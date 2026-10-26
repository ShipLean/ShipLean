using ShipLean.Application.WorkItems;
using ShipLean.Application.WorkItems.History;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Fakes.WorkItems;

// Applies a WorkItemChanges to a ProposedChange and checks the result against the same rules the schema enforces.
internal static class InMemoryChanges
{
    public static ProposedChange From(WorkItemSnapshot item) => new(
        item.Id, item.ProjectId, item.Type, item.Title, item.Description, item.AcceptanceCriteria, item.ReproSteps, item.Severity,
        item.StateId, item.AssigneeId, item.SprintId, item.Priority, item.Points, item.ParentId, item.Tags,
        item.ColumnId, item.ColumnDone, item.LaneId, item.ColumnOrder, item.BacklogOrder,
        item.Goal, item.StatusLine, item.StatusVerifiedAt);

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

    public static void Validate(
        ProposedChange change,
        InMemoryWorld world,
        IReadOnlyDictionary<Guid, UserRef> users,
        IReadOnlyDictionary<Guid, SprintRef> sprints)
    {
        FeatureFieldRules.Check(change.Type, change.Goal, change.StatusLine, change.StatusVerifiedAt);
        if (string.IsNullOrWhiteSpace(change.Title))
        {
            throw ApiException.Validation("A work item needs a title.");
        }

        if (change.Priority is < 1 or > 4 || change.Severity is < 1 or > 4 || change.Points is < 0)
        {
            throw ApiException.Validation("Priority and severity run from 1 to 4, and points can't be negative.");
        }

        if (!world.Projects[change.ProjectId].States.Any(s => s.Id == change.StateId && s.Type == change.Type))
        {
            throw ApiException.Validation("That state doesn't belong to this type of work item.");
        }

        if (change.AssigneeId is { } assignee && !users.ContainsKey(assignee))
        {
            throw ApiException.NotFound("User not found.");
        }

        if (change.SprintId is { } sprint && !sprints.ContainsKey(sprint))
        {
            throw ApiException.NotFound("Sprint not found.");
        }

        ValidateParent(change, world);
    }

    private static void ValidateParent(ProposedChange change, InMemoryWorld world)
    {
        if (change.ParentId is not { } parentId)
        {
            return;
        }

        if (!world.Items.TryGetValue(parentId, out var parent) || parent.ProjectId != change.ProjectId)
        {
            throw ApiException.NotFound("Parent not found.");
        }

        if (parentId == change.WorkItemId || !IsAllowedParent(change.Type, parent.Type))
        {
            throw ApiException.Validation($"A {change.Type} can't have a {parent.Type} as its parent.");
        }
    }

    public static bool IsAllowedParent(WorkItemTypeId type, WorkItemTypeId parent) => type switch
    {
        WorkItemTypeId.Feature => parent is WorkItemTypeId.Epic or WorkItemTypeId.Feature,
        WorkItemTypeId.Story or WorkItemTypeId.Bug => parent == WorkItemTypeId.Feature,
        WorkItemTypeId.Task => parent is WorkItemTypeId.Story or WorkItemTypeId.Bug,
        _ => false,
    };
}
