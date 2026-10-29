using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Views;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Taskboard;

// The grid the sprint Taskboard and the Feature board share: columns, parent states, row cells, the Resolve offer and the People rows.
internal static class TaskboardGrid
{
    // The Task states, Removed left out: the board's columns.
    public static IReadOnlyList<StateRef> TaskStates(BoardSettingsDto board)
        => [.. board.States.First(s => s.Type == WorkItemTypeId.Task).States.Where(s => s.Category != StateCategory.Removed)];

    // The Story and Bug states for the row header's state picker, Removed left out.
    public static IReadOnlyList<BoardTypeStatesDto> ParentStates(BoardSettingsDto board)
        => [.. board.States.Where(s => s.Type is WorkItemTypeId.Story or WorkItemTypeId.Bug)
            .Select(s => s with { States = [.. s.States.Where(state => state.Category != StateCategory.Removed)] })];

    public static IReadOnlyList<TaskboardColumnDto> Columns(IReadOnlyList<StateRef> states, IReadOnlyList<CardDto> taskCards)
        => [.. states.Select(state => new TaskboardColumnDto(state, taskCards.Count(t => t.State.Id == state.Id)))];

    // OfferResolve: the row has tasks, all closed, and its parent hasn't reached Resolved.
    public static bool OfferResolve(StackRowDto parent, IReadOnlyList<CardDto> rowTasks)
        => rowTasks.Count > 0 && rowTasks.All(t => t.State.Category == StateCategory.Completed) && parent.State.Category is StateCategory.Proposed or StateCategory.InProgress;

    public static TaskboardRowDto Row(StackRowDto parent, IReadOnlyList<CardDto> rowTasks, IReadOnlyList<StateRef> states, IReadOnlyDictionary<Guid, CardDto> byId)
        => new(byId[parent.Id], OfferResolve(parent, rowTasks), Cells(rowTasks, states));

    // A People cell mixes parents, so its tasks go in parent row order (tasks without a parent last) and then in cell order.
    public static IReadOnlyList<CardDto> InRowOrder(IReadOnlyList<CardDto> taskCards, IReadOnlyList<Guid> parentIds)
        => [.. taskCards.OrderBy(t => t.Parent is { } parent ? ((List<Guid>)[.. parentIds]).IndexOf(parent.Id) : parentIds.Count)];

    // Unassigned first, then the people who have tasks, by name.
    public static IReadOnlyList<TaskboardPersonRowDto> PeopleRows(IReadOnlyList<CardDto> taskCards, IReadOnlyList<StateRef> states)
    {
        var people = taskCards.Where(t => t.Assignee is not null).GroupBy(t => t.Assignee!.Id)
            .Select(g => g.ToList()).OrderBy(g => g[0].Assignee!.Name, StringComparer.OrdinalIgnoreCase).ThenBy(g => g[0].Assignee!.Id).ToList();
        var unassigned = taskCards.Where(t => t.Assignee is null).ToList();
        return [new TaskboardPersonRowDto(null, unassigned.Count, Cells(unassigned, states)), .. people.Select(g => new TaskboardPersonRowDto(g[0].Assignee, g.Count, Cells(g, states)))];
    }

    public static IReadOnlyList<TaskboardCellDto> Cells(IReadOnlyList<CardDto> rowTasks, IReadOnlyList<StateRef> states)
        => [.. states.Select(state => new TaskboardCellDto(state.Id, [.. rowTasks.Where(t => t.State.Id == state.Id)]))];
}
