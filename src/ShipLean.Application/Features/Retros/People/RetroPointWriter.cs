using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Store;
using ShipLean.Application.Features.Retros.Theming;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// Adds, moves and archives people's points. A person's retro is one per person and target: the person's retro on an item, or
// the person's sprint-level retro. Moving a theme is allowed on any point and changes nothing else about it.
internal sealed class RetroPointWriter(NpgsqlDataSource dataSource, PickRetroThemer pick, IRetroThemer themer, IRetroCards cards, ICurrentUser user, IProjectEvents events)
{
    public async Task<RetroPointDto> AddAsync(Guid projectId, RetroPointCreate request, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var retro = await ResolveRetroAsync(connection, transaction, projectId, request, cancellationToken);
        var text = request.Text.Trim();
        var theme = await ChooseThemeAsync(connection, transaction, projectId, request.ThemeId, request.ThemeName, text, request.RootCause, cancellationToken);
        var position = await connection.QuerySingleAsync<int>(
            new CommandDefinition("SELECT COALESCE(max(position) + 1, 0) FROM retro_point WHERE retrospective_id = @retroId", new { retroId = retro.Id }, transaction, cancellationToken: cancellationToken));
        var pointId = await RetroPointRows.InsertAsync(connection, transaction, retro.Id, request.Category, text, request.RootCause, theme, user.Id, null, position, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        await events.PublishAsync(new RetroChanged(projectId, retro.SprintId is { } sprint ? [sprint] : [], retro.WorkItemId is { } item ? [item] : []));
        return await RetroPointReply.LoadAsync(cards, retro.Id, pointId, cancellationToken);
    }

    public async Task<RetroPointDto> MoveThemeAsync(Guid pointId, RetroPointThemePut request, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        // The themes are locked before the point row (a Merge takes them in that order), then the point is read again under its lock.
        var unlocked = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken, lockPoint: false);
        await RetroThemeRows.LockAsync(connection, transaction, new[] { unlocked.ThemeId, request.ThemeId }.OfType<Guid>().ToArray(), cancellationToken);
        var point = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken);
        if (point.ThemeId != unlocked.ThemeId)
        {
            throw ApiException.Conflict("The point's theme changed. Try again.");
        }

        var theme = await ChooseNamedThemeAsync(connection, transaction, point.ProjectId, request.ThemeId, request.ThemeName, point.Text, cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE retro_point SET theme_id = @themeId, theme_set_by = @setBy WHERE id = @pointId",
                new { pointId, themeId = theme.ThemeId, setBy = theme.ThemeId is null ? null : "person" },
                transaction,
                cancellationToken: cancellationToken));
        if (point.ThemeId != theme.ThemeId)
        {
            var changes = new List<(Guid ThemeId, int Delta, bool Required)>();
            if (point.ThemeId is { } old)
            {
                changes.Add((old, -1, false));
            }
            if (theme.ThemeId is { } added)
            {
                changes.Add((added, 1, true));
            }

            await RetroThemeRows.AdjustAllAsync(connection, transaction, changes, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(point.Changed);
        return await RetroPointReply.LoadAsync(cards, point.RetroId, pointId, cancellationToken);
    }

    public async Task ArchiveAsync(Guid pointId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var unlocked = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken, lockPoint: false);
        await RetroThemeRows.LockAsync(connection, transaction, new[] { unlocked.ThemeId }.OfType<Guid>().ToArray(), cancellationToken);
        var point = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken);
        if (point.ThemeId != unlocked.ThemeId)
        {
            throw ApiException.Conflict("The point's theme changed. Try again.");
        }

        if (point.Role != "person")
        {
            throw ApiException.Validation("Only a point a person wrote can be archived.", "point");
        }

        await connection.ExecuteAsync(new CommandDefinition("UPDATE retro_point SET archived_at = now() WHERE id = @pointId", new { pointId }, transaction, cancellationToken: cancellationToken));
        if (point.ThemeId is { } themeId)
        {
            await RetroThemeRows.AdjustAllAsync(connection, transaction, [(themeId, -1, false)], cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(point.Changed);
    }

    // A named theme goes through the pick themer in any mode, so the person's choice is kept as typed. No theme at all is
    // left to the themer only in auto mode.
    private async Task<RetroThemeResult> ChooseThemeAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid? themeId, string? themeName, string text, string? rootCause, CancellationToken cancellationToken)
    {
        if (themeId is null && string.IsNullOrWhiteSpace(themeName) && themer.Mode == RetroThemeMode.Auto)
        {
            return await themer.AssignAsync(new RetroThemeRequest(projectId, text, rootCause, null, true), cancellationToken);
        }

        return await ChooseNamedThemeAsync(connection, transaction, projectId, themeId, themeName, text, cancellationToken);
    }

    private async Task<RetroThemeResult> ChooseNamedThemeAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid? themeId, string? themeName, string text, CancellationToken cancellationToken)
    {
        if (themeId is { } id)
        {
            await RetroThemeRows.RequireLiveAsync(connection, transaction, projectId, id, cancellationToken);
            return new RetroThemeResult(id, RetroThemeSetBy.Person, null, null);
        }

        // The pick runs on its own connection, so a merge could retire the theme before this transaction inserts the point.
        var picked = await pick.AssignAsync(new RetroThemeRequest(projectId, text, null, themeName, true), cancellationToken);
        if (picked.ThemeId is { } pickedId)
        {
            await RetroThemeRows.RequireLiveAsync(connection, transaction, projectId, pickedId, cancellationToken);
        }
        return picked;
    }

    private async Task<RetroRow> ResolveRetroAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, RetroPointCreate request, CancellationToken cancellationToken)
    {
        if (request.RetroId is { } retroId)
        {
            var existing = await connection.QuerySingleOrDefaultAsync<RetroRow>(
                new CommandDefinition(
                    "SELECT id, role, sprint_id AS sprintId, work_item_id AS workItemId FROM retrospective WHERE id = @retroId AND project_id = @projectId AND archived_at IS NULL",
                    new { retroId, projectId },
                    transaction,
                    cancellationToken: cancellationToken))
                ?? throw ApiException.NotFound("No retro with that id was found.");
            return existing.Role == "person" ? existing : throw ApiException.Validation("Points are added to a person's retro, not to an agent's.", "retroId");
        }

        Guid? sprintId;
        Guid? itemId = null;
        if (request.WorkItemId is { } workItemId)
        {
            await RequireRetroItemAsync(connection, transaction, projectId, workItemId, cancellationToken);
            itemId = workItemId;
            sprintId = await RetroSprints.ResolveAsync(connection, workItemId, null, cancellationToken);
        }
        else
        {
            sprintId = request.SprintId;
            var sprintFound = await connection.ExecuteScalarAsync<bool>(
                new CommandDefinition("SELECT EXISTS (SELECT 1 FROM sprint WHERE id = @sprintId AND project_id = @projectId)", new { sprintId, projectId }, transaction, cancellationToken: cancellationToken));
            if (!sprintFound)
            {
                throw ApiException.NotFound("No sprint with that id was found.");
            }
        }

        // One person's retro on one target: two quick adds must not each create it.
        await connection.ExecuteAsync(
            new CommandDefinition("SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", new { key = $"retro-person:{user.Id}:{itemId ?? sprintId}" }, transaction, cancellationToken: cancellationToken));
        var found = await connection.QuerySingleOrDefaultAsync<RetroRow>(
            new CommandDefinition(
                """
                SELECT id, role, sprint_id AS sprintId, work_item_id AS workItemId FROM retrospective
                WHERE project_id = @projectId AND role = 'person' AND author_user_id = @userId AND archived_at IS NULL
                  AND ((@itemId::uuid IS NOT NULL AND work_item_id = @itemId) OR (@itemId::uuid IS NULL AND work_item_id IS NULL AND sprint_id = @sprintId))
                ORDER BY created_at LIMIT 1
                """,
                new { projectId, userId = user.Id, itemId, sprintId },
                transaction,
                cancellationToken: cancellationToken));
        if (found is not null)
        {
            return found;
        }

        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            new CommandDefinition(
                "INSERT INTO retrospective (id, project_id, work_item_id, sprint_id, author_user_id, role) VALUES (@id, @projectId, @itemId, @sprintId, @userId, 'person')",
                new { id, projectId, itemId, sprintId, userId = user.Id },
                transaction,
                cancellationToken: cancellationToken));
        return new RetroRow(id, "person", sprintId, itemId);
    }

    private static async Task RequireRetroItemAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid workItemId, CancellationToken cancellationToken)
    {
        var type = await connection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition("SELECT type FROM work_item WHERE id = @workItemId AND project_id = @projectId", new { workItemId, projectId }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No work item with that id was found.");
        if (type != "feature" && !WorkItemTypeRules.StoryLevelDb.Contains(type))
        {
            throw ApiException.Validation("Only a story, bug, action item or Feature has a retrospective.", "workItemId");
        }
    }

    private sealed record RetroRow(Guid Id, string Role, Guid? SprintId, Guid? WorkItemId);
}
