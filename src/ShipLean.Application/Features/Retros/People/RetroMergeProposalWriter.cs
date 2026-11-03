using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// Theme merge proposals (K5). Finding the themes, locking them and inserting are one transaction, so a theme a Merge retires meanwhile
// cannot get a proposal. The unique pair index decides a race between two proposals: whoever inserts second gets false.
internal sealed class RetroMergeProposalWriter(NpgsqlDataSource dataSource, IProjectEvents events)
{
    public async Task<bool> ProposeAsync(Guid projectId, string fromTheme, string intoTheme, string reason, RetroActor actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw ApiException.Validation("Say why the themes should merge.", "reason");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var from = await FindAsync(connection, transaction, projectId, fromTheme, "fromTheme", cancellationToken);
        var into = await FindAsync(connection, transaction, projectId, intoTheme, "intoTheme", cancellationToken);
        if (from == into)
        {
            throw ApiException.Validation("Pick two different themes.", "intoTheme");
        }

        await RetroThemeRows.LockAsync(connection, transaction, [from, into], cancellationToken);
        await RequireStillLiveAsync(connection, transaction, projectId, from, "fromTheme", cancellationToken);
        await RequireStillLiveAsync(connection, transaction, projectId, into, "intoTheme", cancellationToken);
        var inserted = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO retro_theme_merge_proposal (id, project_id, from_theme_id, into_theme_id, reason, proposed_by_user_id, proposed_agent_name)
                VALUES (@id, @projectId, @from, @into, @reason, @userId, @agentName)
                ON CONFLICT (LEAST(from_theme_id, into_theme_id), GREATEST(from_theme_id, into_theme_id)) WHERE archived_at IS NULL DO NOTHING
                """,
                new { id = Guid.NewGuid(), projectId, from, into, reason = reason.Trim(), userId = actor.UserId, agentName = actor.AgentName },
                transaction,
                cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        if (inserted == 0)
        {
            return false;
        }

        await events.PublishAsync(new RetroChanged(projectId, [], []));
        return true;
    }

    private static async Task<Guid> FindAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, string name, string field, CancellationToken cancellationToken)
        => await connection.QueryFirstOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT id FROM retro_theme WHERE project_id = @projectId AND lower(name) = lower(@name) AND archived_at IS NULL AND merged_into_id IS NULL ORDER BY created_at LIMIT 1",
                new { projectId, name = name.Trim() },
                transaction,
                cancellationToken: cancellationToken))
            ?? throw ApiException.Validation("No theme with that name was found.", field);

    // The themes are locked now, so what this reads cannot change before the insert.
    private static async Task RequireStillLiveAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, Guid themeId, string field, CancellationToken cancellationToken)
    {
        var live = await connection.ExecuteScalarAsync<bool>(
            new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM retro_theme WHERE id = @themeId AND project_id = @projectId AND archived_at IS NULL AND merged_into_id IS NULL)",
                new { themeId, projectId },
                transaction,
                cancellationToken: cancellationToken));
        if (!live)
        {
            throw ApiException.Validation("No theme with that name was found.", field);
        }
    }
}
