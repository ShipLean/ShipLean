using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// Renames and merges themes. Neither changes a point, so their events name no sprint or item: every open retro view refetches.
internal sealed class RetroThemeWriter(NpgsqlDataSource dataSource, IProjectEvents events)
{
    private const string UniqueViolation = "23505";

    public async Task<RetroThemeRefDto> RenameAsync(Guid themeId, RetroThemeRename request, CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var theme = await connection.QuerySingleOrDefaultAsync<ThemeRow>(
            new CommandDefinition(
                "SELECT id, project_id AS projectId, name, tone FROM retro_theme WHERE id = @themeId AND archived_at IS NULL AND merged_into_id IS NULL FOR UPDATE",
                new { themeId },
                transaction,
                cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No theme with that id was found.");
        var taken = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM retro_theme WHERE project_id = @projectId AND lower(name) = lower(@name) AND id <> @themeId AND archived_at IS NULL AND merged_into_id IS NULL)",
                new { projectId = theme.ProjectId, name, themeId },
                transaction,
                cancellationToken: cancellationToken));
        if (taken)
        {
            throw ApiException.Conflict("Another theme already has that name.");
        }

        try
        {
            await connection.ExecuteAsync(
                new CommandDefinition("UPDATE retro_theme SET name = @name, name_set_by = 'person' WHERE id = @themeId", new { name, themeId }, transaction, cancellationToken: cancellationToken));
            await transaction.CommitAsync(cancellationToken);
        }
        catch (PostgresException exception) when (exception.SqlState == UniqueViolation)
        {
            throw ApiException.Conflict("Another theme already has that name.");
        }

        await events.PublishAsync(new RetroChanged(theme.ProjectId, [], []));
        return new RetroThemeRefDto(themeId, name, Enum.Parse<RetroTone>(theme.Tone, ignoreCase: true));
    }

    // The sources' points and action items move to the target, their counts are added to it, and each source points at it
    // through merged_into_id so a pick by a source's name still lands there.
    public async Task MergeAsync(Guid projectId, RetroThemeMerge request, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await MergeInAsync(connection, transaction, projectId, request, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new RetroChanged(projectId, [], []));
    }

    // The same merge inside a transaction the caller owns, so a caller can decide something in the same commit. It neither commits nor publishes.
    public static async Task MergeInAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, RetroThemeMerge request, CancellationToken cancellationToken)
    {
        var sources = request.SourceIds.Distinct().ToArray();
        var all = sources.Append(request.TargetId).ToArray();
        var live = (await connection.QueryAsync<Guid>(
            new CommandDefinition(
                "SELECT id FROM retro_theme WHERE id = ANY(@all) AND project_id = @projectId AND archived_at IS NULL AND merged_into_id IS NULL ORDER BY id FOR UPDATE",
                new { all, projectId },
                transaction,
                cancellationToken: cancellationToken))).ToList();
        if (live.Count != all.Length)
        {
            throw ApiException.NotFound("No theme with that id was found.");
        }

        var moved = await connection.ExecuteScalarAsync<int>(
            new CommandDefinition("SELECT COALESCE(sum(point_count), 0)::int FROM retro_theme WHERE id = ANY(@sources)", new { sources }, transaction, cancellationToken: cancellationToken));
        var target = request.TargetId;
        await connection.ExecuteAsync(new CommandDefinition("UPDATE retro_point SET theme_id = @target WHERE theme_id = ANY(@sources)", new { target, sources }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition("UPDATE retro_action SET theme_id = @target WHERE theme_id = ANY(@sources)", new { target, sources }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(
            new CommandDefinition("UPDATE retro_theme SET merged_into_id = @target, point_count = 0 WHERE id = ANY(@sources)", new { target, sources }, transaction, cancellationToken: cancellationToken));
        await RetroThemeRows.AdjustAllAsync(connection, transaction, [(target, moved, true)], cancellationToken);
    }

    private sealed record ThemeRow(Guid Id, Guid ProjectId, string Name, string Tone);
}
