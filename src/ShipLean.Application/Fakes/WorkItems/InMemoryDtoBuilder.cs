using ShipLean.Application.Markdown;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Fakes.WorkItems;

// Builds the read shapes (WorkItemDto, RevisionDto, ConflictDto) from the fake's stored snapshots and the arranged
// users and sprints.
internal sealed class InMemoryDtoBuilder(
    InMemoryWorld world,
    IReadOnlyDictionary<Guid, UserRef> users,
    IReadOnlyDictionary<Guid, SprintRef> sprints,
    MarkdownRenderer renderer,
    TimeProvider time)
{
    private static readonly Dictionary<WorkItemTypeId, string> TypeNames = new()
    {
        [WorkItemTypeId.Epic] = "Epic",
        [WorkItemTypeId.Feature] = "Feature",
        [WorkItemTypeId.Story] = "User Story",
        [WorkItemTypeId.Bug] = "Bug",
        [WorkItemTypeId.Task] = "Task",
    };

    public ConflictDto Conflict(WorkItemSnapshot current, int expectedRevision)
    {
        var theirs = world.Revisions[current.Id]
            .Where(r => r.Rev > expectedRevision)
            .OrderByDescending(r => r.Rev)
            .Select(Revision)
            .ToList();
        return new ConflictDto("This work item was changed by someone else.", ApiErrorCodes.Conflict, Item(current), theirs);
    }

    public RevisionDto Revision(RevisionSnapshot revision) => new(
        revision.Rev,
        UserOrNull(revision.ChangedBy),
        revision.ChangedAt,
        DisplayDate.Relative(revision.ChangedAt, time),
        revision.Origin,
        revision.Changes,
        revision.AgentName,
        revision.Webhook);

    public WorkItemDto Item(WorkItemSnapshot item)
    {
        var project = world.Projects[item.ProjectId];
        var description = renderer.Render(item.Description);
        var repro = renderer.Render(item.ReproSteps);
        return new WorkItemDto(
            item.Id,
            item.ProjectId,
            item.Key,
            item.Number,
            TypeRef(item.Type),
            item.Title,
            description.Markdown,
            description.Html,
            item.AcceptanceCriteria,
            repro.Markdown,
            repro.Html,
            item.Severity,
            State(project, item.StateId),
            UserOrNull(item.AssigneeId),
            UserOrNull(item.CreatedBy),
            UserOrNull(item.ChangedBy),
            item.SprintId is { } sprint ? sprints[sprint] : null,
            item.Priority,
            item.Points,
            item.ParentId is { } parent ? Parent(world.Items[parent]) : null,
            item.Tags,
            new BoardPositionDto(item.ColumnId, item.ColumnDone, item.LaneId, item.ColumnOrder),
            item.BacklogOrder,
            item.Links.Select(Link).ToList(),
            Children(item, project),
            item.OriginalSystem,
            item.Revision,
            item.CreatedAt,
            DisplayDate.Relative(item.CreatedAt, time),
            item.ChangedAt,
            DisplayDate.Relative(item.ChangedAt, time),
            item.Goal,
            item.StatusLine,
            item.StatusVerifiedAt is { } verified
                ? new StatusVerifiedDto(
                    verified, DisplayDate.Relative(verified, time), StatusVerifiedDto.IsStale(verified, time.GetUtcNow()),
                    UserOrNull(item.StatusVerifiedBy), item.StatusVerifiedAgent, item.StatusVerifiedVia)
                : null,
            item.Slug,
            item.StartedOn,
            item.StartedOn is { } started ? DisplayDate.ShortDay(DisplayDate.StartOfDay(started, time.LocalTimeZone), time.GetUtcNow(), time.LocalTimeZone) : null);
    }

    private static TypeRef TypeRef(WorkItemTypeId type) => new(type, TypeNames[type]);

    private static StateRef State(InMemoryProject project, Guid id)
    {
        var state = project.States.First(s => s.Id == id);
        return new StateRef(state.Id, state.Name, state.Category);
    }

    private UserRef? UserOrNull(Guid? id) => id is { } value && users.TryGetValue(value, out var user) ? user : null;

    private static ParentRefDto Parent(WorkItemSnapshot parent) => new(parent.Id, parent.Key, TypeRef(parent.Type), parent.Title);

    private LinkDto Link(LinkSnapshot link)
    {
        var value = link.Value;
        var target = value.WorkItemId is { } id ? world.Items[id] : null;
        return new LinkDto(link.Id, value.Type, value.WorkItemId, target?.Key, target?.Title ?? value.Title, value.Url);
    }

    private List<ChildSummaryDto> Children(WorkItemSnapshot item, InMemoryProject project)
        => world.Items.Values
            .Where(child => child.ParentId == item.Id)
            .OrderBy(child => child.BacklogOrder, StringComparer.Ordinal)
            .Select(child => new ChildSummaryDto(
                child.Id, child.Key, TypeRef(child.Type), child.Title, State(project, child.StateId), UserOrNull(child.AssigneeId), child.Points))
            .ToList();
}
