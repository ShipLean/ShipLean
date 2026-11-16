using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

// A submitted or working session that has been silent for 30 minutes becomes stale (Linear's own rule). One idempotent UPDATE, safe on every
// instance and on every pass: a session that spoke since is no longer silent, and one already stale or ended is not working. A submitted one the agent never answered (its last activity is its creation) is swept too, so it frees the item's live slot; a stale session is not live and never blocks Start.
// The same UPDATE records end_reason stale and when the session last spoke (UTC, to the second); an activity that revives it clears both.
public sealed class AgentStaleSweeper(NpgsqlDataSource dataSource, AgentSessionNotifier notifier, TimeProvider time)
{
    public static readonly TimeSpan Silence = TimeSpan.FromMinutes(30);

    // Returns how many sessions it marked.
    public async Task<int> SweepAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var stale = (await connection.QueryAsync<Guid>(new CommandDefinition(
            """
            UPDATE agent_session
            SET state = 'stale', end_reason = @reason,
                end_detail = 'No activity for 30 minutes since ' || to_char(last_activity_at AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS"Z"') || '.'
            WHERE state IN ('submitted', 'working') AND last_activity_at < @cutoff
            RETURNING id
            """,
            new { cutoff = time.GetUtcNow() - Silence, reason = AgentEndReasons.Stale }, cancellationToken: cancellationToken))).ToList();
        foreach (var id in stale)
        {
            await notifier.ChangedAsync(id);
        }

        return stale.Count;
    }
}
