using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.Contracts.WorkItems;
using ShipLean.Core;

namespace ShipLean.Application.Features.Sprints;

public sealed partial class SprintStore
{
    private static readonly string UnfinishedItems =
        $"""
        SELECT w.id, w.key, w.title, w.type, s.id AS stateId, s.name AS stateName, s.category AS stateCategory,
               u.id AS assigneeId, u.name AS assigneeName, u.initials AS assigneeInitials, u.avatar_colour AS assigneeColour, {UserRefs.PhotoUrlSql} AS assigneePhotoUrl,
               w.story_points AS points,
               (SELECT count(*) FROM work_item t JOIN state ts ON ts.id = t.state_id
                WHERE t.parent_id = w.id AND t.sprint_id = w.sprint_id AND t.type = 'task' AND ts.category NOT IN ('completed', 'removed'))::int AS openTasks,
               (SELECT count(*) FROM work_item t JOIN state ts ON ts.id = t.state_id
                WHERE t.parent_id = w.id AND t.sprint_id = w.sprint_id AND t.type = 'task' AND ts.category <> 'removed')::int AS totalTasks
        FROM work_item w JOIN state s ON s.id = w.state_id LEFT JOIN app_user u ON u.id = w.assigned_to
        WHERE w.sprint_id = @sprintId AND w.type IN ('story', 'bug') AND s.category NOT IN ('completed', 'removed')
        ORDER BY w.backlog_order, w.number
        """;

    public async Task<RolloverDto> GetRolloverAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var sprint = await LoadAsync(connection, null, sprintId, forUpdate: false, cancellationToken);
        var next = await connection.QueryFirstOrDefaultAsync<SprintRow>(new CommandDefinition(
            $"SELECT {SprintRow.Columns} FROM sprint WHERE project_id = @projectId AND archived_at IS NULL AND start_date > @end ORDER BY start_date LIMIT 1",
            new { projectId = sprint.ProjectId, end = sprint.End }, cancellationToken: cancellationToken));
        var items = (await connection.QueryAsync<RolloverItemRow>(new CommandDefinition(UnfinishedItems, new { sprintId }, cancellationToken: cancellationToken))).ToList();
        decimal? nextCapacity = null;
        var nextPoints = 0m;
        if (next is not null)
        {
            nextCapacity = await connection.ExecuteScalarAsync<decimal?>(new CommandDefinition(
                "SELECT sum(capacity_points) FROM sprint_capacity WHERE sprint_id = @id AND archived_at IS NULL", new { next.Id }, cancellationToken: cancellationToken));
            nextPoints = await connection.ExecuteScalarAsync<decimal>(new CommandDefinition(
                """
                SELECT coalesce(sum(w.story_points), 0) FROM work_item w JOIN state s ON s.id = w.state_id
                WHERE w.sprint_id = @id AND w.type IN ('story', 'bug') AND s.category <> 'removed'
                """,
                new { next.Id }, cancellationToken: cancellationToken));
        }

        var today = SprintLabels.Today(clock);
        var status = sprint.RolloverStatus is null ? RolloverStatus.Pending : Enum.Parse<RolloverStatus>(sprint.RolloverStatus, true);
        if (status == RolloverStatus.Pending && sprint.End < today && items.Count == 0)
        {
            // An ended sprint with nothing unfinished has nothing to decide: the first read marks it decided, so no prompt is ever asked for it.
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE sprint SET rollover_status = 'decided', updated_at = @now WHERE id = @sprintId AND rollover_status IS DISTINCT FROM 'decided'",
                new { sprintId, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
            await events.PublishAsync(new SprintChanged(sprint.ProjectId, sprintId));
            status = RolloverStatus.Decided;
        }

        return new RolloverDto(
            Ref(sprint, today), status,
            [.. items.Select(i => i.ToDto(next?.Id))], next is null ? null : Ref(next, today), nextCapacity, nextPoints);
    }

    private const int MAX_ATTEMPTS = 3;

    // Open tasks of the moved stories that are still in the ended sprint.
    private const string OpenTasksOfSql =
        """
        SELECT t.id, t.key, t.sprint_id AS sprintId, t.revision, t.parent_id AS parentId
        FROM work_item t JOIN state ts ON ts.id = t.state_id
        WHERE t.parent_id = ANY(@parents) AND t.type = 'task' AND t.sprint_id = @sprintId AND t.id <> ALL(@explicitIds) AND ts.category NOT IN ('completed', 'removed')
        ORDER BY t.key
        """;

    // Every move goes through IWorkItemStore.PatchManyAsync, and the hook marks the sprint decided inside that one transaction.
    // A moved story takes its open tasks of this sprint with it: they are read here and checked again inside the write's transaction
    // (after its locks), so a task closed, added or moved meanwhile makes the attempt start over with a fresh read.
    public async Task<SprintDto> RolloverAsync(Guid sprintId, RolloverRequest request, CancellationToken cancellationToken = default)
    {
        if (request?.Moves is null || request.Moves.Any(m => m is null))
        {
            throw ApiException.Validation("A rollover needs its list of moves.");
        }

        SprintRow sprint;
        var attempt = 0;
        while (true)
        {
            attempt++;
            Dictionary<Guid, RolloverTarget> items;
            IReadOnlyList<TaskRow> tasks;

            // The read connection is returned to the pool before the work item store opens its own.
            await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
            {
                sprint = await LoadAsync(connection, null, sprintId, forUpdate: false, cancellationToken);
                if (sprint.End > SprintLabels.Today(clock))
                {
                    throw ApiException.Validation("The sprint has not ended.");
                }

                if (string.Equals(sprint.RolloverStatus, "decided", StringComparison.OrdinalIgnoreCase))
                {
                    throw ApiException.Conflict("This sprint's unfinished items have already been dealt with.");
                }

                items = await CheckMovesAsync(connection, sprint, request, cancellationToken);
                tasks = await OpenTasksAsync(connection, null, sprintId, request.Moves, cancellationToken);
            }

            var targets = request.Moves.ToDictionary(m => m.WorkItemId, m => m.TargetSprintId);
            var patches = request.Moves
                .Select(move => (move.WorkItemId, MoveTo(items[move.WorkItemId].Revision, move.TargetSprintId)))
                .Concat(tasks.Select(task => (task.Id, MoveTo(task.Revision, targets[task.ParentId]))))
                .ToList();
            var toBacklog = request.Moves.Where(m => m.TargetSprintId is null).Select(m => m.WorkItemId).ToArray();
            var planned = tasks.ToDictionary(t => t.Id, t => t.Revision);
            var options = new WriteOptions(
                async (transaction, token) =>
                {
                    await transaction.Connection!.ExecuteAsync(new CommandDefinition(
                        "UPDATE sprint SET rollover_status = 'decided', updated_at = @now WHERE id = @sprintId",
                        new { sprintId, now = clock.GetUtcNow() }, transaction, cancellationToken: token));
                    await PutOnTopOfBacklogAsync(transaction, sprint.ProjectId, toBacklog, token);
                },
                RevisionOrigin.User,
                async (transaction, token) =>
                {
                    var now = await OpenTasksAsync(transaction.Connection!, transaction, sprintId, request.Moves, token);
                    if (now.Count != planned.Count || now.Any(t => !planned.TryGetValue(t.Id, out var revision) || revision != t.Revision))
                    {
                        throw new RolloverTasksChangedException();
                    }
                })
            { Bulk = true };

            WriteManyOutcome outcome;
            try
            {
                outcome = await workItems.PatchManyAsync(patches, options, cancellationToken);
            }
            catch (RolloverTasksChangedException) when (attempt < MAX_ATTEMPTS)
            {
                continue;
            }
            catch (RolloverTasksChangedException)
            {
                throw ApiException.Conflict("The sprint's tasks kept changing while the rollover was applied. Nothing was moved; try again.");
            }

            if (outcome.Conflicts is { Count: > 0 } stale)
            {
                if (attempt < MAX_ATTEMPTS && stale.All(c => planned.ContainsKey(c.Current.Id)))
                {
                    continue;
                }

                throw ApiException.Conflict($"These items changed since the rollover was loaded: {string.Join(", ", stale.Select(c => c.Current.Key))}. Nothing was moved.");
            }

            break;
        }

        await events.PublishAsync(new SprintChanged(sprint.ProjectId, sprintId));
        await using var reload = await dataSource.OpenConnectionAsync(cancellationToken);
        return (await LoadAsync(reload, null, sprintId, forUpdate: false, cancellationToken)).ToDto(SprintLabels.Today(clock));
    }

    private static WorkItemPatch MoveTo(int revision, Guid? target) => new(revision, new WorkItemChanges { Sprint = new FieldSet<Guid?>(target) });

    private static async Task<IReadOnlyList<TaskRow>> OpenTasksAsync(
        System.Data.Common.DbConnection connection, System.Data.Common.DbTransaction? transaction, Guid sprintId, IReadOnlyList<RolloverMove> moves, CancellationToken cancellationToken)
        => [.. await connection.QueryAsync<TaskRow>(new CommandDefinition(
            OpenTasksOfSql, new { parents = moves.Select(m => m.WorkItemId).ToArray(), explicitIds = moves.Select(m => m.WorkItemId).ToArray(), sprintId }, transaction, cancellationToken: cancellationToken))];

    // Items sent back to the backlog go above everything else in it, in the order they were sent.
    private static async Task PutOnTopOfBacklogAsync(System.Data.Common.DbTransaction transaction, Guid projectId, Guid[] ids, CancellationToken cancellationToken)
    {
        if (ids.Length == 0)
        {
            return;
        }

        var top = await transaction.Connection!.ExecuteScalarAsync<string?>(new CommandDefinition(
            "SELECT min(backlog_order) FROM work_item WHERE project_id = @projectId AND id <> ALL(@ids)", new { projectId, ids }, transaction, cancellationToken: cancellationToken));
        var keys = new string[ids.Length];
        for (var i = ids.Length - 1; i >= 0; i--)
        {
            top = keys[i] = Rank.Between(null, top);
        }

        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            "UPDATE work_item w SET backlog_order = v.key FROM unnest(@ids, @keys) AS v(id, key) WHERE w.id = v.id", new { ids, keys }, transaction, cancellationToken: cancellationToken));
    }

    private sealed record TaskRow(Guid Id, string Key, Guid? SprintId, int Revision, Guid ParentId);

    private static async Task<Dictionary<Guid, RolloverTarget>> CheckMovesAsync(
        NpgsqlConnection connection, SprintRow sprint, RolloverRequest request, CancellationToken cancellationToken)
    {
        var ids = request.Moves.Select(m => m.WorkItemId).ToArray();
        if (ids.Distinct().Count() != ids.Length)
        {
            throw ApiException.Validation("An item can only be moved once.");
        }

        var items = (await connection.QueryAsync<RolloverTarget>(new CommandDefinition(
            "SELECT id, key, sprint_id AS sprintId, revision FROM work_item WHERE id = ANY(@ids)", new { ids }, cancellationToken: cancellationToken))).ToDictionary(i => i.Id);
        if (items.Count != ids.Length)
        {
            throw ApiException.NotFound("There is no such work item.");
        }

        if (items.Values.Any(i => i.SprintId != sprint.Id))
        {
            throw ApiException.Validation("That item is not in the sprint.");
        }

        var targets = request.Moves.Where(m => m.TargetSprintId is not null).Select(m => m.TargetSprintId!.Value).Distinct().ToArray();
        var found = (await connection.QueryAsync<(Guid Id, Guid ProjectId)>(new CommandDefinition(
            "SELECT id, project_id AS projectId FROM sprint WHERE id = ANY(@targets) AND archived_at IS NULL", new { targets }, cancellationToken: cancellationToken))).ToList();
        if (found.Count != targets.Length)
        {
            throw ApiException.NotFound("There is no such target sprint.");
        }

        if (found.Any(t => t.ProjectId != sprint.ProjectId))
        {
            throw ApiException.Validation("The target sprint belongs to another project.");
        }

        return items;
    }

    private static SprintRef Ref(SprintRow sprint, DateOnly today) => new(sprint.Id, sprint.Name, SprintLabels.Phase(sprint.Start, sprint.End, today).ToStatus());
}
