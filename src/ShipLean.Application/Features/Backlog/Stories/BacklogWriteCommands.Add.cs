using ShipLean.Contracts;
using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Backlog.Stories;

public sealed partial class BacklogWriteCommands
{
    public async Task<BacklogAddResult> AddAsync(Guid projectId, BacklogAddRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        RequireWithinLimit(request.Items.Sum(i => 1 + i.Tasks.Count), "rows, counting tasks,");
        CheckPlacement(request.Placement);
        foreach (var item in request.Items)
        {
            CheckAddItem(item);
        }

        var items = request.Items.Select(i => new WorkItemBlockCreate.Item(
            Create(projectId, i.Type, i.Title, i.ParentId, request.SprintId, i.Points, i.Description),
            [.. i.Tasks.Select(t => Create(projectId, WorkItemTypeId.Task, t.Title, null, request.SprintId, t.Points, null))])).ToList();
        var placement = request.Placement is { } p ? new BatchPlacement(p.Slot, p.BeforeItemId) : null;
        var created = await blocks.CreateBlockAsync(new WorkItemBlockCreate(projectId, placement, items), WriteOptions.User, cancellationToken);
        return new BacklogAddResult(created);
    }

    private static void CheckAddItem(BacklogAddItem item)
    {
        if (item.Type == WorkItemTypeId.Task)
        {
            throw ApiException.Validation("A task goes under a story, not at the top level.");
        }

        if (string.IsNullOrWhiteSpace(item.Title) || item.Tasks.Any(t => string.IsNullOrWhiteSpace(t.Title)))
        {
            throw ApiException.Validation("Every row needs a title.", "title");
        }
    }

    private static WorkItemCreate Create(Guid projectId, WorkItemTypeId type, string title, Guid? parentId, Guid? sprintId, decimal? points, string? description)
    {
        if (points is null && description is null)
        {
            return new WorkItemCreate(projectId, type, title.Trim(), parentId, sprintId, null);
        }

        var changes = new WorkItemChanges
        {
            Points = points is { } p ? new FieldSet<decimal?>(p) : null,
            Description = description is { } d ? new FieldSet<string>(d) : null,
        };
        return new WorkItemCreate(projectId, type, title.Trim(), parentId, sprintId, changes);
    }

    public async Task<DuplicateCheckResult> DuplicatesAsync(Guid projectId, DuplicateCheckRequest request, CancellationToken cancellationToken)
    {
        await access.RequireAsync(projectId, ProjectRole.Member, cancellationToken);
        RequireWithinLimit(request.Titles.Count, "titles");
        return new DuplicateCheckResult(await checks.DuplicatesAsync(projectId, request.Titles, cancellationToken));
    }
}
