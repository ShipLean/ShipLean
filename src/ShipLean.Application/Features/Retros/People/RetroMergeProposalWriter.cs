using Dapper;
using Npgsql;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.People;

// Theme merge proposals (K5). The unique pair index decides a race: whoever inserts second gets false.
internal sealed class RetroMergeProposalWriter(NpgsqlDataSource dataSource)
{
    public async Task<bool> ProposeAsync(Guid projectId, string fromTheme, string intoTheme, string reason, RetroActor actor, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(reason))
        {
            throw ApiException.Validation("Say why the themes should merge.", "reason");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var from = await FindAsync(connection, projectId, fromTheme, "fromTheme", cancellationToken);
        var into = await FindAsync(connection, projectId, intoTheme, "intoTheme", cancellationToken);
        if (from == into)
        {
            throw ApiException.Validation("Pick two different themes.", "intoTheme");
        }

        var inserted = await connection.ExecuteAsync(
            new CommandDefinition(
                """
                INSERT INTO retro_theme_merge_proposal (id, project_id, from_theme_id, into_theme_id, reason, proposed_by_user_id, proposed_agent_name)
                VALUES (@id, @projectId, @from, @into, @reason, @userId, @agentName)
                ON CONFLICT (LEAST(from_theme_id, into_theme_id), GREATEST(from_theme_id, into_theme_id)) WHERE archived_at IS NULL DO NOTHING
                """,
                new { id = Guid.NewGuid(), projectId, from, into, reason = reason.Trim(), userId = actor.UserId, agentName = actor.AgentName },
                cancellationToken: cancellationToken));
        return inserted > 0;
    }

    private static async Task<Guid> FindAsync(NpgsqlConnection connection, Guid projectId, string name, string field, CancellationToken cancellationToken)
        => await connection.QueryFirstOrDefaultAsync<Guid?>(
            new CommandDefinition(
                "SELECT id FROM retro_theme WHERE project_id = @projectId AND lower(name) = lower(@name) AND archived_at IS NULL AND merged_into_id IS NULL ORDER BY created_at LIMIT 1",
                new { projectId, name = name.Trim() },
                cancellationToken: cancellationToken))
            ?? throw ApiException.Validation("No theme with that name was found.", field);
}
