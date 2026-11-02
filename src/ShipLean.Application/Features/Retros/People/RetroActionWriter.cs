using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Retros.People;

// Turns a Can be improved point into an Action item. The item is made by the work item store (its first Proposed state, no
// parent) and linked to the point and the point's theme. A point whose earlier action item was Removed takes the new one over.
internal sealed class RetroActionWriter(NpgsqlDataSource dataSource, IWorkItemStore store, IRetroCards cards, IProjectEvents events)
{
    private const int MaxDerivedTitle = 200;
    private const string UniqueViolation = "23505";

    public async Task<RetroActionRefDto> CreateAsync(Guid pointId, RetroActionCreate request, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        RetroPointInfo point;
        Guid? previous;
        // This transaction only reads, to fail early with a clear 409. It is not the guard: two requests can both pass it, and
        // what stops the second is the ux_retro_action_point_live index (insert) or the affected-row count of archiving the Removed link.
        await using (var check = await connection.BeginTransactionAsync(cancellationToken))
        {
            point = await RetroPointInfo.LoadAsync(connection, check, pointId, cancellationToken);
            if (!string.Equals(point.Category, "improve", StringComparison.Ordinal))
            {
                throw ApiException.Validation("Only a Can be improved point becomes an action item.", "point");
            }

            previous = await PreviousActionAsync(connection, check, pointId, cancellationToken);
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? DerivedTitle(point.Text) : request.Title.Trim();
        var changes = request.Points is { } points ? new WorkItemChanges { Points = new FieldSet<decimal?>(points) } : null;
        var outcome = await store.CreateAsync(new WorkItemCreate(point.ProjectId, WorkItemTypeId.Action, title, null, null, changes), WriteOptions.User, cancellationToken);
        var created = outcome.Written ?? throw ApiException.Internal();

        bool linked;
        try
        {
            linked = await LinkAsync(connection, point, previous, created.Id, cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolation)
        {
            linked = false;
        }

        if (!linked)
        {
            // Another request linked the point first; the item made here has no link, so it is removed rather than left loose.
            await store.RemoveAsync(created.Id, created.Revision, cancellationToken);
            throw ApiException.Conflict("This point already has an action item.");
        }

        await events.PublishAsync(point.Changed);
        var reply = await RetroPointReply.LoadAsync(cards, point.RetroId, pointId, cancellationToken);
        return reply.Action ?? throw ApiException.Internal();
    }

    private static string DerivedTitle(string text)
    {
        var line = text.Trim().Split('\n')[0].Trim();
        return line.Length <= MaxDerivedTitle ? line : line[..MaxDerivedTitle].TrimEnd();
    }

    // The action item the point already has, unless it was Removed; a live one is a 409.
    private static async Task<Guid?> PreviousActionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid pointId, CancellationToken cancellationToken)
    {
        var existing = await connection.QuerySingleOrDefaultAsync<(Guid WorkItemId, string Category)?>(
            new CommandDefinition(
                """
                SELECT rap.work_item_id AS workItemId, st.category
                FROM retro_action_point rap JOIN work_item w ON w.id = rap.work_item_id JOIN state st ON st.id = w.state_id
                WHERE rap.point_id = @pointId AND rap.archived_at IS NULL
                """,
                new { pointId },
                transaction,
                cancellationToken: cancellationToken));
        if (existing is null)
        {
            return null;
        }

        return existing.Value.Category == "removed" ? existing.Value.WorkItemId : throw ApiException.Conflict("This point already has an action item.");
    }

    // One transaction: the action row (point_id stays null, K1), the archive of the Removed item's link when there is one, and the new link.
    private async Task<bool> LinkAsync(NpgsqlConnection connection, RetroPointInfo point, Guid? previous, Guid workItemId, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO retro_action (work_item_id, project_id, theme_id, raised_sprint_id) VALUES (@workItemId, @projectId, @themeId, @sprintId)",
            new { workItemId, projectId = point.ProjectId, themeId = point.ThemeId, sprintId = point.SprintId },
            transaction,
            cancellationToken: cancellationToken));
        if (previous is not null)
        {
            var archived = await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE retro_action_point SET archived_at = now() WHERE work_item_id = @previous AND point_id = @pointId AND archived_at IS NULL",
                new { previous, pointId = point.Id },
                transaction,
                cancellationToken: cancellationToken));
            if (archived != 1)
            {
                return false;
            }
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO retro_action_point (work_item_id, point_id) VALUES (@workItemId, @pointId)",
            new { workItemId, pointId = point.Id },
            transaction,
            cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    // TODO(A0W): fill in per K5. The projectId, the actor and the transaction rules are in contracts.md.
    public async Task<RetroActionDetailDto> CreateActionAsync(Guid projectId, RetroActionNew request, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        throw ApiException.Internal();
    }

    // TODO(A0W): fill in per K5.
    public async Task<RetroActionDetailDto> AddPointsAsync(Guid workItemId, RetroActionAddPoints request, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        throw ApiException.Internal();
    }

    // TODO(A0W): fill in per K5.
    public async Task<RetroActionDetailDto> UpdateActionAsync(Guid workItemId, RetroActionPut request, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        throw ApiException.Internal();
    }
}
