using Agoda.IoC.Core;
using ShipLean.Contracts;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Taskboard;

// The sprint's Taskboard: rows are the sprint's stories and bugs in stack order and then the Unparented row, columns are the
// project's Task states (Removed left out). The same tasks are also grouped by assignee. Callers read it as a member.
[RegisterPerRequest]
public sealed class TaskboardViews(IProjectAccess access, IBoardSettingsStore boards, IWorkTreeReads tree, TaskboardTasks tasks, TaskboardCards cards)
{
    public async Task<TaskboardViewDto> GetAsync(Guid sprintId, CancellationToken cancellationToken)
    {
        var (projectId, _) = await access.RequireAsync(ProjectItem.Sprint, sprintId, ProjectRole.Member, cancellationToken);
        var summary = (await boards.ListAsync(projectId, cancellationToken)).FirstOrDefault(b => b.Level == BoardLevel.Taskboard)
            ?? throw ApiException.NotFound("The project has no Taskboard.");
        var board = await boards.GetAsync(summary.Id, cancellationToken);

        var parents = (await tree.StackAsync(projectId, StackLevel.Requirement, null, true, cancellationToken)).Where(row => row.Sprint?.Id == sprintId).ToList();
        var parentIds = parents.Select(row => row.Id).ToList();
        var taskRows = await tasks.ForSprintAsync(projectId, sprintId, parentIds, cancellationToken);
        var rollups = await tree.RollupsAsync(projectId, parentIds, null, cancellationToken);
        var byId = await cards.BuildAsync([.. parents, .. taskRows], rollups, board.Settings, cancellationToken);

        var states = board.States.First(s => s.Type == WorkItemTypeId.Task).States.Where(s => s.Category != StateCategory.Removed).ToList();
        var taskCards = taskRows.Select(row => byId[row.Id]).ToList();
        var rows = parents.Select(parent => Row(parent, taskCards.Where(t => t.Parent?.Id == parent.Id).ToList(), states, byId)).ToList();
        rows.Add(new TaskboardRowDto(null, false, Cells(taskCards.Where(t => t.Parent is null).ToList(), states)));

        var parentStates = board.States.Where(s => s.Type is WorkItemTypeId.Story or WorkItemTypeId.Bug)
            .Select(s => s with { States = [.. s.States.Where(state => state.Category != StateCategory.Removed)] }).ToList();
        return new TaskboardViewDto(
            sprintId, board.Id,
            [.. states.Select(state => new TaskboardColumnDto(state, taskCards.Count(t => t.State.Id == state.Id)))],
            rows, PeopleRows(InRowOrder(taskCards, parentIds), states), parentStates, board.Settings.StyleRules, board.Settings.CardFields, board.Settings.Annotations);
    }

    // OfferResolve: the row has tasks, all closed, and its parent hasn't reached Resolved.
    private static TaskboardRowDto Row(StackRowDto parent, List<CardDto> rowTasks, List<StateRef> states, IReadOnlyDictionary<Guid, CardDto> byId)
    {
        var offer = rowTasks.Count > 0 && rowTasks.All(t => t.State.Category == StateCategory.Completed) && parent.State.Category is StateCategory.Proposed or StateCategory.InProgress;
        return new TaskboardRowDto(byId[parent.Id], offer, Cells(rowTasks, states));
    }

    // A People cell mixes parents, so its tasks go in parent row order (the Unparented row's last) and then in cell order.
    private static List<CardDto> InRowOrder(List<CardDto> taskCards, List<Guid> parentIds)
        => [.. taskCards.OrderBy(t => t.Parent is { } parent ? parentIds.IndexOf(parent.Id) : parentIds.Count)];

    // Unassigned first, then the people who have tasks, by name.
    private static List<TaskboardPersonRowDto> PeopleRows(List<CardDto> taskCards, List<StateRef> states)
    {
        var people = taskCards.Where(t => t.Assignee is not null).GroupBy(t => t.Assignee!.Id)
            .Select(g => g.ToList()).OrderBy(g => g[0].Assignee!.Name, StringComparer.OrdinalIgnoreCase).ThenBy(g => g[0].Assignee!.Id).ToList();
        var unassigned = taskCards.Where(t => t.Assignee is null).ToList();
        return [new TaskboardPersonRowDto(null, unassigned.Count, Cells(unassigned, states)), .. people.Select(g => new TaskboardPersonRowDto(g[0].Assignee, g.Count, Cells(g, states)))];
    }

    private static List<TaskboardCellDto> Cells(List<CardDto> rowTasks, List<StateRef> states)
        => [.. states.Select(state => new TaskboardCellDto(state.Id, [.. rowTasks.Where(t => t.State.Id == state.Id)]))];
}
