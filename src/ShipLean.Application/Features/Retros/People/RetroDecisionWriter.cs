using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Theming;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// Not needed and agent theme assignment (K5). Both lock the point row first; AssignThemeAsync takes the themes before it, in the
// order MoveThemeAsync does, so it cannot deadlock against a Move or a Merge.
internal sealed class RetroDecisionWriter(NpgsqlDataSource dataSource, PickRetroThemer pick, IRetroCards cards, IProjectEvents events)
{
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

    // False, with nothing changed, when a person set the point's theme.
    public async Task<bool> AssignThemeAsync(Guid pointId, string themeName, RetroActor actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(themeName))
        {
            throw ApiException.Validation("Name the theme.", "themeName");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var read = await connection.BeginTransactionAsync(cancellationToken);
        var first = await RetroPointInfo.LoadAsync(connection, read, pointId, cancellationToken, lockPoint: false);
        if (await SetByPersonAsync(connection, read, pointId, cancellationToken))
        {
            return false;
        }

        await read.CommitAsync(cancellationToken);

        // The pick runs on its own connection, so the theme is checked again under its lock below.
        var picked = await pick.AssignAsync(new RetroThemeRequest(first.ProjectId, string.Empty, null, themeName.Trim(), false), cancellationToken);
        var themeId = picked.ThemeId ?? throw ApiException.Validation("Name the theme.", "themeName");

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await RetroThemeRows.LockAsync(connection, transaction, new[] { first.ThemeId, themeId }.OfType<Guid>().ToArray(), cancellationToken);
        var point = await RetroPointInfo.LoadAsync(connection, transaction, pointId, cancellationToken);
        if (point.ThemeId != first.ThemeId)
        {
            throw ApiException.Conflict("The point's theme changed. Try again.");
        }

        if (await SetByPersonAsync(connection, transaction, pointId, cancellationToken))
        {
            return false;
        }

        await RetroThemeRows.RequireLiveAsync(connection, transaction, point.ProjectId, themeId, cancellationToken);
        await connection.ExecuteAsync(
            new CommandDefinition("UPDATE retro_point SET theme_id = @themeId, theme_set_by = 'agent' WHERE id = @pointId", new { pointId, themeId }, transaction, cancellationToken: cancellationToken));
        if (point.ThemeId != themeId)
        {
            var changes = new List<(Guid ThemeId, int Delta, bool Required)>();
            if (point.ThemeId is { } old)
            {
                changes.Add((old, -1, false));
            }

            changes.Add((themeId, 1, true));
            await RetroThemeRows.AdjustAllAsync(connection, transaction, changes, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(point.Changed);
        return true;
    }

    private static Task<bool> SetByPersonAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid pointId, CancellationToken cancellationToken)
        => connection.ExecuteScalarAsync<bool>(
            new CommandDefinition("SELECT theme_set_by = 'person' FROM retro_point WHERE id = @pointId", new { pointId }, transaction, cancellationToken: cancellationToken));

    // The decision comes from the cards, the one reader of the K2 rule; the point's row lock is held, so it cannot change meanwhile.
    private async Task<RetroDecision?> DecisionAsync(RetroPointInfo point, CancellationToken cancellationToken)
        => (await RetroPointReply.LoadAsync(cards, point.RetroId, point.Id, cancellationToken)).Decision;
}
