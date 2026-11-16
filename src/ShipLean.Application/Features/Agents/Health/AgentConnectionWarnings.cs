using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Core;

namespace ShipLean.Application.Features.Agents.Health;

// The Start agent and Create demo warning for a connection whose newest delivery attempt failed and is newer than its newest success.
public sealed class AgentConnectionWarnings(NpgsqlDataSource dataSource, TimeProvider time) : IAgentConnectionWarnings
{
    public async Task<string?> ForConnectionAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var newest = await connection.QuerySingleOrDefaultAsync<Newest>(new CommandDefinition(
            """
            SELECT at, (error IS NULL) AS succeeded FROM agent_delivery_attempt
            WHERE connection_id = @connectionId ORDER BY at DESC, (error IS NULL) DESC LIMIT 1
            """,
            new { connectionId }, cancellationToken: cancellationToken));
        return newest is { Succeeded: false } failed
            ? $"The last delivery to this connection failed {DisplayDate.Relative(failed.At, time)}."
            : null;
    }

    // A send succeeded when its error is null (a 202 the deliverer rejected has one; so has no answer). On a tie of time the success wins,
    // so the query is exactly "newest failure newer than newest success".
    private sealed record Newest(DateTimeOffset At, bool Succeeded);
}
