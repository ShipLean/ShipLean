using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Theming;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.Application.Features.Retros.People;

// Action items over retro points (K5). The item is made by the work item store (its first Proposed state, no sprint, no parent) and
// linked to its points by join rows; a point whose earlier action item was Removed is taken over by archiving that join row.
// The unique index on live join rows is the guard against two writes racing for one point.
internal sealed class RetroActionWriter(NpgsqlDataSource dataSource, IWorkItemStore store, PickRetroThemer pick, RetroActionDetailReader details, IProjectEvents events)
{
    private const int MaxDerivedTitle = 200;
    private const string UniqueViolation = "23505";
    private const string AlreadyDecided = "This point is already decided.";

    // The old one-point route: the point's own theme, no fix type, the title from the point's text when none is given.
    public async Task<RetroActionRefDto> CreateAsync(Guid pointId, RetroActionCreate request, CancellationToken cancellationToken)
    {
        RetroPointInfo point;
        await using (var connection = await dataSource.OpenConnectionAsync(cancellationToken))
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            point = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken, lockPoint: false);
        }

        var title = string.IsNullOrWhiteSpace(request.Title) ? DerivedTitle(point.Text) : request.Title.Trim();
        var detail = await CreateCoreAsync(point.ProjectId, title, request.Points, null, [pointId], null, null, cancellationToken);
        return detail.Action;
    }

    public Task<RetroActionDetailDto> CreateActionAsync(Guid projectId, RetroActionNew request, RetroActor actor, CancellationToken cancellationToken)
        => CreateCoreAsync(projectId, request.Title.Trim(), null, request.FixType, request.PointIds, request.ThemeId, request.ThemeName, cancellationToken);

    public async Task<RetroActionDetailDto> AddPointsAsync(Guid workItemId, RetroActionAddPoints request, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IReadOnlyDictionary<Guid, Guid> previous;
        ActionRow action;
        await using (var check = await connection.BeginTransactionAsync(cancellationToken))
        {
            action = await LiveActionAsync(connection, check, workItemId, cancellationToken);
            var points = await RetroActionPoint.LoadAsync(connection, check, action.ProjectId, request.PointIds, false, cancellationToken);
            previous = RetroActionPoint.RequireOpen(points);
        }

        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            try
            {
                // Under the points' locks, a Not needed or another link made since the check above is seen here.
                action = await LiveActionAsync(connection, transaction, workItemId, cancellationToken);
                RetroActionPoint.RequireOpen(await RetroActionPoint.LoadAsync(connection, transaction, action.ProjectId, request.PointIds, true, cancellationToken));
                var position = await connection.QuerySingleAsync<int>(
                    new CommandDefinition("SELECT COALESCE(max(position) + 1, 0) FROM retro_action_point WHERE work_item_id = @workItemId", new { workItemId }, transaction, cancellationToken: cancellationToken));
                await LinkPointsAsync(connection, transaction, workItemId, request.PointIds, previous, position, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (PostgresException exception) when (exception.SqlState == UniqueViolation)
            {
                throw ApiException.Conflict(AlreadyDecided);
            }
        }

        return await PublishedAsync(action.ProjectId, workItemId, cancellationToken);
    }

    public async Task<RetroActionDetailDto> UpdateActionAsync(Guid workItemId, RetroActionPut request, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        Guid projectId;
        await using (var check = await connection.BeginTransactionAsync(cancellationToken))
        {
            var current = await LiveActionAsync(connection, check, workItemId, cancellationToken);
            projectId = current.ProjectId;
            RequireFixType(request, current);
            if (request.ThemeId is { } id)
            {
                await RetroThemeRows.RequireLiveAsync(connection, check, current.ProjectId, id, cancellationToken);
            }
        }

        var themeId = await ResolveThemeAsync(projectId, request.ThemeId, request.ThemeName, cancellationToken);
        await using (var transaction = await connection.BeginTransactionAsync(cancellationToken))
        {
            var action = await LiveActionAsync(connection, transaction, workItemId, cancellationToken);
            RequireFixType(request, action);
            if (themeId is { } theme)
            {
                await RetroThemeRows.RequireLiveAsync(connection, transaction, action.ProjectId, theme, cancellationToken);
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE retro_action SET fix_type = @fixType, theme_id = @themeId WHERE work_item_id = @workItemId",
                new { workItemId, fixType = FixTypeText(request.FixType), themeId },
                transaction,
                cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }

        return await PublishedAsync(projectId, workItemId, cancellationToken);
    }

    private async Task<RetroActionDetailDto> CreateCoreAsync(
        Guid projectId, string title, decimal? storyPoints, RetroFixType? fixType, IReadOnlyList<Guid> pointIds, Guid? themeId, string? themeName, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        IReadOnlyList<RetroActionPoint> points;
        IReadOnlyDictionary<Guid, Guid> previous;
        // This transaction only reads, to fail early with a clear error before an item is made. It is not the guard: two requests
        // can both pass it, and what stops the second is the live-link index (insert) or the affected-row count of archiving a Removed link.
        await using (var check = await connection.BeginTransactionAsync(cancellationToken))
        {
            points = await RetroActionPoint.LoadAsync(connection, check, projectId, pointIds, false, cancellationToken);
            previous = RetroActionPoint.RequireOpen(points);
            if (themeId is { } given)
            {
                await RetroThemeRows.RequireLiveAsync(connection, check, projectId, given, cancellationToken);
            }
        }

        var chosen = await ResolveThemeAsync(projectId, themeId, themeName, cancellationToken);
        var theme = chosen ?? Commonest(points);
        var changes = storyPoints is { } storyPoint ? new WorkItemChanges { Points = new FieldSet<decimal?>(storyPoint) } : null;
        var outcome = await store.CreateAsync(new WorkItemCreate(projectId, WorkItemTypeId.Action, title, null, null, changes), WriteOptions.User, cancellationToken);
        var created = outcome.Written ?? throw ApiException.Internal();

        try
        {
            await LinkAsync(connection, projectId, created.Id, theme, chosen, fixType, points, pointIds, previous, cancellationToken);
        }
        catch (ApiException)
        {
            // The item made here has no link, so it is removed rather than left loose.
            await store.RemoveAsync(created.Id, created.Revision, cancellationToken);
            throw;
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolation)
        {
            await store.RemoveAsync(created.Id, created.Revision, cancellationToken);
            throw ApiException.Conflict(AlreadyDecided);
        }

        return await PublishedAsync(projectId, created.Id, cancellationToken);
    }

    // One transaction: the action row (point_id stays null, K1), then each point's link, archiving the Removed item's join row first.
    private static async Task LinkAsync(
        NpgsqlConnection connection, Guid projectId, Guid workItemId, Guid? themeId, Guid? requiredTheme, RetroFixType? fixType,
        IReadOnlyList<RetroActionPoint> points, IReadOnlyList<Guid> pointIds, IReadOnlyDictionary<Guid, Guid> previous, CancellationToken cancellationToken)
    {
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        if (requiredTheme is { } required)
        {
            await RetroThemeRows.RequireLiveAsync(connection, transaction, projectId, required, cancellationToken);
        }

        // Locks the points, so a Not needed made since the check is seen here as a conflict.
        RetroActionPoint.RequireOpen(await RetroActionPoint.LoadAsync(connection, transaction, projectId, pointIds, true, cancellationToken));
        var raisedSprint = points.Select(point => point.SprintId).FirstOrDefault(sprint => sprint is not null);
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO retro_action (work_item_id, project_id, theme_id, raised_sprint_id, fix_type) VALUES (@workItemId, @projectId, @themeId, @raisedSprint, @fixType)",
            new { workItemId, projectId, themeId, raisedSprint, fixType = FixTypeText(fixType) },
            transaction,
            cancellationToken: cancellationToken));
        await LinkPointsAsync(connection, transaction, workItemId, pointIds, previous, 0, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    private static async Task LinkPointsAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, IReadOnlyList<Guid> pointIds, IReadOnlyDictionary<Guid, Guid> previous, int firstPosition, CancellationToken cancellationToken)
    {
        for (var index = 0; index < pointIds.Count; index++)
        {
            var pointId = pointIds[index];
            if (previous.TryGetValue(pointId, out var earlier))
            {
                var archived = await connection.ExecuteAsync(new CommandDefinition(
                    "UPDATE retro_action_point SET archived_at = now() WHERE work_item_id = @earlier AND point_id = @pointId AND archived_at IS NULL",
                    new { earlier, pointId },
                    transaction,
                    cancellationToken: cancellationToken));
                if (archived != 1)
                {
                    throw ApiException.Conflict(AlreadyDecided);
                }
            }

            // An archived row for this very pair (the point was taken over from this item once) comes back to life.
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO retro_action_point (work_item_id, point_id, position) VALUES (@workItemId, @pointId, @position)
                ON CONFLICT (work_item_id, point_id) DO UPDATE SET archived_at = NULL, position = EXCLUDED.position
                """,
                new { workItemId, pointId, position = firstPosition + index },
                transaction,
                cancellationToken: cancellationToken));
        }
    }

    private async Task<RetroActionDetailDto> PublishedAsync(Guid projectId, Guid workItemId, CancellationToken cancellationToken)
    {
        var detail = await details.LoadAsync(workItemId, cancellationToken);
        var sprints = detail.Points.Select(point => point.Sprint?.Id).OfType<Guid>().Distinct().ToList();
        var items = detail.Points.Select(point => point.WorkItem?.Id).OfType<Guid>().Distinct().ToList();
        await events.PublishAsync(new RetroChanged(projectId, sprints, items));
        return detail;
    }

    // The theme the caller named: by id as given, or by name through the pick themer (a new name makes a theme). Null when none was named.
    private async Task<Guid?> ResolveThemeAsync(Guid projectId, Guid? themeId, string? themeName, CancellationToken cancellationToken)
    {
        if (themeId is not null || string.IsNullOrWhiteSpace(themeName))
        {
            return themeId;
        }

        var picked = await pick.AssignAsync(new RetroThemeRequest(projectId, themeName, null, themeName, true), cancellationToken);
        return picked.ThemeId;
    }

    // The theme most of the points have; on a tie the one met first in the order given.
    private static Guid? Commonest(IReadOnlyList<RetroActionPoint> points)
        => points.Select(point => point.ThemeId).OfType<Guid>().GroupBy(id => id).MaxBy(group => group.Count())?.Key;

    private static void RequireFixType(RetroActionPut request, ActionRow action)
    {
        if (request.FixType is null && action.FixType is not null)
        {
            throw ApiException.Validation("An action item that has a type of fix keeps one.", "fixType");
        }
    }

    private static string? FixTypeText(RetroFixType? fixType) => fixType?.ToString().ToLowerInvariant();

    private static string DerivedTitle(string text)
    {
        var line = text.Trim().Split('\n')[0].Trim();
        return line.Length <= MaxDerivedTitle ? line : line[..MaxDerivedTitle].TrimEnd();
    }

    // The action item must exist (not_found) and be a retro action that is not Removed (validation, field workItemId).
    private static async Task<ActionRow> LiveActionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT w.project_id AS projectId, ra.fix_type AS fixType, st.category AS stateCategory, (ra.work_item_id IS NOT NULL) AS isAction
            FROM work_item w JOIN state st ON st.id = w.state_id LEFT JOIN retro_action ra ON ra.work_item_id = w.id
            WHERE w.id = @workItemId
            """;
        var row = await connection.QuerySingleOrDefaultAsync<ActionRow>(new CommandDefinition(sql, new { workItemId }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No action item with that id was found.");
        return row.IsAction && row.StateCategory != "removed" ? row : throw ApiException.Validation("That is not an action item raised from a retro.", "workItemId");
    }

    private sealed record ActionRow(Guid ProjectId, string? FixType, string StateCategory, bool IsAction);
}
