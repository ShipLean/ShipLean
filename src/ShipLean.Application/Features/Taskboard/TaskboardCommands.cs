using Agoda.IoC.Core;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Taskboard;
using ShipLean.Contracts.WorkItems;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Taskboard;

// A drop on the Taskboard: the column writes the task's state, the row its parent (Stories) or assignee (People), and the
// store writes both in one revision (a task dropped on the Unparented row takes the sprint, which is how an unparented task belongs to one) and orders the card in its (state, parent) cell.
// A People drop ignores BeforeItemId: a People cell mixes parents, so the card goes last in its (state, parent) cell.
// The task, the row target and the prior state are read on a connection of their own, outside the store's transaction and lock, so
// a concurrent change can make them stale. OfferResolve is a hint (the GET is the source of truth) and may be stale for the same reason.
[RegisterPerRequest]
public sealed class TaskboardCommands(IProjectAccess access, IWorkItemStore store, IWorkTreeReads tree, NpgsqlDataSource dataSource)
{
    public async Task<TaskboardMoveOutcome> MoveAsync(Guid sprintId, TaskboardMoveRequest request, CancellationToken cancellationToken)
    {
        var (projectId, _) = await access.RequireAsync(ProjectItem.Sprint, sprintId, ProjectRole.Member, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var task = await connection.QuerySingleOrDefaultAsync<TaskboardMoveRecord>(new CommandDefinition(
            """
            SELECT w.project_id AS projectId, w.type, w.parent_id AS parentId, s.category AS stateCategory
            FROM work_item w JOIN state s ON s.id = w.state_id LEFT JOIN work_item p ON p.id = w.parent_id
            WHERE w.id = @id AND s.category <> 'removed'
              AND (w.parent_id IS NULL AND w.sprint_id = @sprintId
                   OR p.type IN ('story', 'bug') AND p.sprint_id = @sprintId AND p.project_id = w.project_id
                      AND p.state_id IN (SELECT id FROM state WHERE category <> 'removed'))
            """,
            new { id = request.TaskId, sprintId }, cancellationToken: cancellationToken));
        if (task is null || task.ProjectId != projectId || task.Type != "task")
        {
            throw ApiException.NotFound("Task not found.");
        }

        return await WriteAsync(connection, projectId, task, request, sprintId, (grouping, target) => CheckRowAsync(connection, grouping, target, projectId, sprintId, cancellationToken), cancellationToken);
    }

    // The Feature board's drop: the same write, scoped to the stories and bugs in the Feature's tree. The task keeps its own sprint.
    public async Task<TaskboardMoveOutcome> MoveInFeatureAsync(Guid featureId, TaskboardMoveRequest request, CancellationToken cancellationToken)
    {
        var (projectId, _) = await access.RequireAsync(ProjectItem.WorkItem, featureId, ProjectRole.Member, cancellationToken);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var live = await connection.QuerySingleAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM work_item w JOIN state s ON s.id = w.state_id WHERE w.id = @featureId AND w.type = 'feature' AND s.category <> 'removed')",
            new { featureId }, cancellationToken: cancellationToken));
        if (!live)
        {
            throw ApiException.NotFound("Feature not found.");
        }

        var inTree = (await tree.DescendantsAsync(projectId, featureId, null, cancellationToken)).Select(d => d.Id).ToArray();
        var task = await connection.QuerySingleOrDefaultAsync<TaskboardMoveRecord>(new CommandDefinition(
            """
            SELECT w.project_id AS projectId, w.type, w.parent_id AS parentId, s.category AS stateCategory
            FROM work_item w JOIN state s ON s.id = w.state_id JOIN work_item p ON p.id = w.parent_id
            WHERE w.id = @id AND w.type = 'task' AND s.category <> 'removed'
              AND p.type IN ('story', 'bug') AND p.id = ANY(@tree) AND p.project_id = w.project_id
              AND p.state_id IN (SELECT id FROM state WHERE category <> 'removed')
            """,
            new { id = request.TaskId, tree = inTree }, cancellationToken: cancellationToken));
        if (task is null || task.ProjectId != projectId)
        {
            throw ApiException.NotFound("Task not found.");
        }

        if (request.Row is { Grouping: TaskboardGrouping.Stories, Target: null })
        {
            throw ApiException.Validation("A task on the Feature board belongs to a story or bug.");
        }

        return await WriteAsync(connection, projectId, task, request, null, (grouping, target) => CheckFeatureRowAsync(connection, grouping, target, projectId, inTree, cancellationToken), cancellationToken);
    }

    // The shared write: state, then parent or assignee, in one revision. unparentedSprint is the sprint a drop on the Unparented row takes (the sprint scope only).
    private async Task<TaskboardMoveOutcome> WriteAsync(NpgsqlConnection connection, Guid projectId, TaskboardMoveRecord task, TaskboardMoveRequest request, Guid? unparentedSprint, Func<TaskboardGrouping, Guid, Task> checkRow, CancellationToken cancellationToken)
    {
        var closing = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT category FROM state WHERE id = @id AND project_id = @projectId AND type = 'task' AND category <> 'removed' AND archived_at IS NULL",
            new { id = request.StateId, projectId }, cancellationToken: cancellationToken))
            ?? throw ApiException.Validation("That column isn't a Task state.");
        if (request.Row is { Target: { } target } row)
        {
            await checkRow(row.Grouping, target);
        }

        var set = new WorkItemChanges
        {
            State = new FieldSet<Guid>(request.StateId),
            Parent = request.Row is { Grouping: TaskboardGrouping.Stories } stories ? new FieldSet<Guid?>(stories.Target) : null,
            Sprint = unparentedSprint is { } sprintId && request.Row is { Grouping: TaskboardGrouping.Stories, Target: null } ? new FieldSet<Guid?>(sprintId) : null,
            Assignee = request.Row is { Grouping: TaskboardGrouping.People } people ? new FieldSet<Guid?>(people.Target) : null,
        };
        var outcome = await store.MoveAsync(request.TaskId, new MoveTarget(request.ExpectedRevision, null, false, null, request.Row is { Grouping: TaskboardGrouping.People } ? null : request.BeforeItemId, set), WriteOptions.User, cancellationToken);
        if (outcome.Written is not { } written)
        {
            return new TaskboardMoveOutcome(null, outcome.Conflict);
        }

        var parentId = request.Row is { Grouping: TaskboardGrouping.Stories } moved ? moved.Target : task.ParentId;
        var closed = closing == "completed" && (task.StateCategory != "completed" || parentId != task.ParentId);
        var offer = closed && parentId is { } parent && await OfferResolveAsync(connection, parent, cancellationToken);
        return new TaskboardMoveOutcome(new TaskboardMoveResultDto(written, parentId, offer), null);
    }

    private static async Task CheckFeatureRowAsync(NpgsqlConnection connection, TaskboardGrouping grouping, Guid target, Guid projectId, Guid[] tree, CancellationToken cancellationToken)
    {
        var sql = grouping == TaskboardGrouping.Stories
            ? """
              SELECT EXISTS (SELECT 1 FROM work_item w JOIN state s ON s.id = w.state_id
                             WHERE w.id = @target AND w.project_id = @projectId AND w.id = ANY(@tree) AND w.type IN ('story', 'bug') AND s.category <> 'removed')
              """
            : "SELECT EXISTS (SELECT 1 FROM project_member WHERE project_id = @projectId AND user_id = @target AND archived_at IS NULL)";
        if (!await connection.QuerySingleAsync<bool>(new CommandDefinition(sql, new { target, projectId, tree }, cancellationToken: cancellationToken)))
        {
            throw ApiException.NotFound(grouping == TaskboardGrouping.Stories ? "Story or bug not found." : "Member not found.");
        }
    }

    // A row target is a story or bug of this sprint, or a member of the project.
    private static async Task CheckRowAsync(NpgsqlConnection connection, TaskboardGrouping grouping, Guid target, Guid projectId, Guid sprintId, CancellationToken cancellationToken)
    {
        var sql = grouping == TaskboardGrouping.Stories
            ? """
              SELECT EXISTS (SELECT 1 FROM work_item w JOIN state s ON s.id = w.state_id
                             WHERE w.id = @target AND w.project_id = @projectId AND w.sprint_id = @sprintId AND w.type IN ('story', 'bug') AND s.category <> 'removed')
              """
            : "SELECT EXISTS (SELECT 1 FROM project_member WHERE project_id = @projectId AND user_id = @target AND archived_at IS NULL)";
        if (!await connection.QuerySingleAsync<bool>(new CommandDefinition(sql, new { target, projectId, sprintId }, cancellationToken: cancellationToken)))
        {
            throw ApiException.NotFound(grouping == TaskboardGrouping.Stories ? "Story or bug not found." : "Member not found.");
        }
    }

    // The parent has tasks, none of them open, and hasn't reached Resolved.
    private static async Task<bool> OfferResolveAsync(NpgsqlConnection connection, Guid parentId, CancellationToken cancellationToken)
        => await connection.QuerySingleAsync<bool>(new CommandDefinition(
            """
            SELECT p_state.category IN ('proposed', 'inProgress')
               AND EXISTS (SELECT 1 FROM work_item c JOIN state cs ON cs.id = c.state_id WHERE c.parent_id = p.id AND c.type = 'task' AND cs.category <> 'removed')
               AND NOT EXISTS (SELECT 1 FROM work_item c JOIN state cs ON cs.id = c.state_id WHERE c.parent_id = p.id AND c.type = 'task' AND cs.category NOT IN ('completed', 'removed'))
            FROM work_item p JOIN state p_state ON p_state.id = p.state_id WHERE p.id = @parentId
            """, new { parentId }, cancellationToken: cancellationToken));
}
