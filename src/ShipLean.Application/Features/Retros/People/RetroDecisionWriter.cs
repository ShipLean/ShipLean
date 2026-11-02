using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// Not needed and agent theme assignment (K5). Both lock the point row first; AssignThemeAsync takes the themes before it, in the
// order MoveThemeAsync does, so it cannot deadlock against a Move or a Merge.
internal sealed class RetroDecisionWriter(NpgsqlDataSource dataSource, IRetroCards cards, IProjectEvents events)
{
    private const int MaxMergeHops = 20;
    private const string AlreadyDecided = "This point is already decided.";

    public async Task<RetroPointDto> MarkNotNeededAsync(Guid pointId, RetroNotNeededPut request, RetroActor actor, CancellationToken cancellationToken)
    {
        RetroChecks.NotNeededPut(request);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var point = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken);
        if (point.Category == "wentWell")
        {
            throw ApiException.Validation("A point that went well needs no decision.", "pointId");
        }

        if (await DecisionAsync(point, cancellationToken) is not RetroDecision.Undecided)
        {
            throw ApiException.Conflict(AlreadyDecided);
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE retro_point SET not_needed_reason = @reason, decided_by_user_id = @userId, decided_agent_name = @agentName, decided_at = now() WHERE id = @pointId",
                new { pointId, reason = request.Reason.Trim(), userId = actor.UserId, agentName = actor.AgentName },
                transaction,
                cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(point.Changed);
        return await RetroPointReply.LoadAsync(cards, point.RetroId, pointId, cancellationToken);
    }

    public async Task<RetroPointDto> UndoNotNeededAsync(Guid pointId, RetroActor actor, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var point = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken);
        if (await DecisionAsync(point, cancellationToken) is not RetroDecision.NotNeeded)
        {
            throw ApiException.Conflict("This point is not marked Not needed.");
        }

        await connection.ExecuteAsync(
            new CommandDefinition(
                "UPDATE retro_point SET not_needed_reason = NULL, decided_by_user_id = NULL, decided_agent_name = NULL, decided_at = NULL WHERE id = @pointId",
                new { pointId },
                transaction,
                cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(point.Changed);
        return await RetroPointReply.LoadAsync(cards, point.RetroId, pointId, cancellationToken);
    }

    // False, with nothing changed, when a person set the point's theme. Everything, including creating the theme, is one transaction, and
    // nothing is written before the checks under the locks pass. The themes are locked before the point row, as MoveThemeAsync does.
    public async Task<bool> AssignThemeAsync(Guid pointId, string themeName, RetroActor actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            throw ApiException.Validation("Name the theme.", "themeName");
        }

        var name = themeName.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var unlocked = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken, lockPoint: false);
        var existing = await FindThemeAsync(connection, transaction, unlocked.ProjectId, name, cancellationToken);
        await RetroThemeRows.LockAsync(connection, transaction, new[] { unlocked.ThemeId, existing }.OfType<Guid>().ToArray(), cancellationToken);
        var point = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken);
        if (point.ThemeId != unlocked.ThemeId)
        {
            throw ApiException.Conflict("The point's theme changed. Try again.");
        }

        if (await SetByPersonAsync(connection, transaction, pointId, cancellationToken))
        {
            return false;
        }

        var themeId = existing;
        if (themeId is not null)
        {
            await RetroThemeRows.RequireLiveAsync(connection, transaction, point.ProjectId, themeId.Value, cancellationToken);
        }
        else
        {
            themeId = await CreateThemeAsync(connection, transaction, point.ProjectId, name, cancellationToken);
        }

        await connection.ExecuteAsync(
            new CommandDefinition("UPDATE retro_point SET theme_id = @themeId, theme_set_by = 'agent' WHERE id = @pointId", new { pointId, themeId }, transaction, cancellationToken: cancellationToken));
        if (point.ThemeId != themeId)
        {
            var changes = new List<(Guid ThemeId, int Delta, bool Required)>();
            if (point.ThemeId is { } old)
            {
                changes.Add((old, -1, false));
            }

            changes.Add((themeId.Value, 1, true));
            await RetroThemeRows.AdjustAllAsync(connection, transaction, changes, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(point.Changed);
        return true;
    }

    private static Task<bool> SetByPersonAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid pointId, CancellationToken cancellationToken)
        => connection.ExecuteScalarAsync<bool>(
            new CommandDefinition("SELECT theme_set_by = 'person' FROM retro_point WHERE id = @pointId", new { pointId }, transaction, cancellationToken: cancellationToken));

    // A name matches a live theme case-insensitively and follows merged_into_id to the theme that absorbed it, as the pick themer does.
    private static async Task<Guid?> FindThemeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string name, CancellationToken cancellationToken)
    {
        var current = await connection.QueryFirstOrDefaultAsync<ThemeRow>(
            new CommandDefinition(
                """
                SELECT id, merged_into_id AS mergedIntoId FROM retro_theme
                WHERE project_id = @projectId AND lower(name) = lower(@name) AND (archived_at IS NULL OR merged_into_id IS NOT NULL)
                ORDER BY (merged_into_id IS NOT NULL), created_at
                LIMIT 1
                """,
                new { projectId, name },
                transaction,
                cancellationToken: cancellationToken));
        for (var hop = 0; current is { MergedIntoId: { } next } && hop < MaxMergeHops; hop++)
        {
            current = await connection.QuerySingleAsync<ThemeRow>(
                new CommandDefinition("SELECT id, merged_into_id AS mergedIntoId FROM retro_theme WHERE id = @next", new { next }, transaction, cancellationToken: cancellationToken));
        }

        return current?.Id;
    }

    private static async Task<Guid> CreateThemeAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string name, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var created = await connection.ExecuteScalarAsync<Guid?>(
            new CommandDefinition(
                """
                INSERT INTO retro_theme (id, project_id, name, name_set_by, tone)
                VALUES (@id, @projectId, @name, 'agent', 'bad')
                ON CONFLICT (project_id, lower(name)) WHERE archived_at IS NULL AND merged_into_id IS NULL DO NOTHING
                RETURNING id
                """,
                new { id, projectId, name },
                transaction,
                cancellationToken: cancellationToken));
        return created ?? throw ApiException.Conflict("That theme was just created. Try again.");
    }

    // The decision comes from the cards, the one reader of the K2 rule; the point's row lock is held, so it cannot change meanwhile.
    private async Task<RetroDecision?> DecisionAsync(RetroPointInfo point, CancellationToken cancellationToken)
        => (await RetroPointReply.LoadAsync(cards, point.RetroId, point.Id, cancellationToken)).Decision;

    private sealed record ThemeRow(Guid Id, Guid? MergedIntoId);
}
