using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Core;

namespace ShipLean.Application.Features.Agents.Health;

// An agent connection's recent delivery health, read from agent_delivery_attempt: the newest 2xx, the sends that failed in the
// last 24 hours (the time provider's clock), and the newest failure with its scrubbed error. A connection with no attempts is empty.
public sealed class AgentConnectionHealth(NpgsqlDataSource dataSource, TimeProvider time) : IAgentConnectionHealth
{
    public async Task<AgentConnectionHealthDto> ForConnectionAsync(Guid connectionId, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var delivered = await connection.QuerySingleAsync<DateTimeOffset?>(new CommandDefinition(
            "SELECT max(at) FROM agent_delivery_attempt WHERE connection_id = @connectionId AND http_status BETWEEN 200 AND 299",
            new { connectionId }, cancellationToken: cancellationToken));
        var failedLast24h = await connection.QuerySingleAsync<int>(new CommandDefinition(
            """
            SELECT count(*)::int FROM agent_delivery_attempt
            WHERE connection_id = @connectionId AND (http_status IS NULL OR http_status NOT BETWEEN 200 AND 299) AND at > @since
            """,
            new { connectionId, since = now.AddHours(-24) }, cancellationToken: cancellationToken));
        var failed = await connection.QuerySingleOrDefaultAsync<Failure>(new CommandDefinition(
            """
            SELECT at, error FROM agent_delivery_attempt
            WHERE connection_id = @connectionId AND (http_status IS NULL OR http_status NOT BETWEEN 200 AND 299)
            ORDER BY at DESC LIMIT 1
            """,
            new { connectionId }, cancellationToken: cancellationToken));
        return new AgentConnectionHealthDto(
            delivered,
            delivered is { } at ? DisplayDate.Relative(at, time) : null,
            failedLast24h,
            failed?.At,
            failed is null ? null : DisplayDate.Relative(failed.At, time),
            failed?.Error);
    }

    private sealed record Failure(DateTimeOffset At, string? Error);
}
