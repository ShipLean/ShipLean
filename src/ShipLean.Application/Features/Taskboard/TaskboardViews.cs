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

        var states = TaskboardGrid.TaskStates(board);
        var taskCards = taskRows.Select(row => byId[row.Id]).ToList();
        var rows = parents.Select(parent => TaskboardGrid.Row(parent, taskCards.Where(t => t.Parent?.Id == parent.Id).ToList(), states, byId)).ToList();
        rows.Add(new TaskboardRowDto(null, false, TaskboardGrid.Cells(taskCards.Where(t => t.Parent is null).ToList(), states)));

        return new TaskboardViewDto(
            sprintId, board.Id, TaskboardGrid.Columns(states, taskCards),
            rows, TaskboardGrid.PeopleRows(TaskboardGrid.InRowOrder(taskCards, parentIds), states), TaskboardGrid.ParentStates(board),
            board.Settings.StyleRules, board.Settings.CardFields, board.Settings.Annotations);
    }
}
