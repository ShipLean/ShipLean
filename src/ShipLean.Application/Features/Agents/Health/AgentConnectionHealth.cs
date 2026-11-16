using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Core;

namespace ShipLean.Application.Features.Agents.Health;

// An agent connection's recent delivery health, read from agent_delivery_attempt: the newest success, the sends that failed in the
// last 24 hours (the time provider's clock), and the newest failure with its scrubbed error. A connection with no attempts is empty.
// A send succeeded when its error is null, not when its status is 2xx: the deliverer treats only 200 OK as success and writes an error for
// any other answer, a 202 included.
public sealed class AgentConnectionHealth(NpgsqlDataSource dataSource, TimeProvider time, IViewerZone viewerZone) : IAgentConnectionHealth
{
    public async Task<AgentConnectionHealthDto> ForConnectionAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var delivered = await connection.QuerySingleAsync<DateTimeOffset?>(new CommandDefinition(
            "SELECT max(at) FROM agent_delivery_attempt WHERE connection_id = @connectionId AND error IS NULL",
            new { connectionId }, cancellationToken: cancellationToken));
        var failedLast24h = await connection.QuerySingleAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM agent_delivery_attempt
            WHERE connection_id = @connectionId AND error IS NOT NULL AND at > @since
            """,
            new { connectionId, since = now.AddHours(-24) }, cancellationToken: cancellationToken));
        var failed = await connection.QuerySingleOrDefaultAsync<Failure>(new CommandDefinition(
            """
            SELECT at, error FROM agent_delivery_attempt
            WHERE connection_id = @connectionId AND error IS NOT NULL
            ORDER BY at DESC LIMIT 1
            """,
            new { connectionId }, cancellationToken: cancellationToken));
        return new AgentConnectionHealthDto(
            delivered,
            delivered is { } at ? DisplayDate.Relative(at, time.GetUtcNow(), viewerZone.Zone) : null,
            failedLast24h,
            failed?.At,
            failed is null ? null : DisplayDate.Relative(failed.At, time.GetUtcNow(), viewerZone.Zone),
            failed?.Error);
    }

    private sealed record Failure(DateTimeOffset At, string? Error);
}
