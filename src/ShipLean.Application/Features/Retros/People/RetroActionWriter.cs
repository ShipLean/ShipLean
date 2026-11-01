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
        // what stops the second is the ux_retro_action_point index (insert) or the affected-row count of the repoint (Removed).
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
                SELECT ra.work_item_id AS workItemId, st.category
                FROM retro_action ra JOIN work_item w ON w.id = ra.work_item_id JOIN state st ON st.id = w.state_id
                WHERE ra.point_id = @pointId
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

    private async Task<bool> LinkAsync(NpgsqlConnection connection, RetroPointInfo point, Guid? previous, Guid workItemId, CancellationToken cancellationToken)
    {
        var parameters = new { workItemId, previous, projectId = point.ProjectId, themeId = point.ThemeId, pointId = point.Id, sprintId = point.SprintId };
        var sql = previous is null
            ? "INSERT INTO retro_action (work_item_id, project_id, theme_id, point_id, raised_sprint_id) VALUES (@workItemId, @projectId, @themeId, @pointId, @sprintId)"
            : "UPDATE retro_action SET work_item_id = @workItemId, theme_id = @themeId, raised_sprint_id = @sprintId WHERE work_item_id = @previous AND point_id = @pointId";
        return await connection.ExecuteAsync(new CommandDefinition(sql, parameters, cancellationToken: cancellationToken)) == 1;
    }
}
