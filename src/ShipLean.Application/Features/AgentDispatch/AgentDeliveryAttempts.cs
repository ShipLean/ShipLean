using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Core;

namespace ShipLean.Application.Features.AgentDispatch;

// agent_delivery_attempt. ForSessionsAsync reads a session's attempts through their delivery, oldest first (by time, then the
// delivery's seq, then the attempt number), with server labels.
public sealed class AgentDeliveryAttempts(NpgsqlDataSource dataSource, TimeProvider time) : IAgentDeliveryAttempts
{
    // TODO: no-op until the insert (ON CONFLICT (delivery_id, attempt) DO NOTHING, logged and never thrown) replaces it.
    public Task RecordAsync(AgentDeliveryAttemptCreate attempt, CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<ILookup<Guid, AgentDeliveryAttemptDto>> ForSessionsAsync(IReadOnlyCollection<Guid> sessionIds, CancellationToken cancellationToken)
    {
        if (sessionIds.Count == 0)
        {
            return Array.Empty<Row>().ToLookup(r => r.SessionId, ToDto);
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<Row>(new CommandDefinition(
            """
            SELECT d.session_id AS sessionId, d.action, a.attempt, a.at, a.http_status AS httpStatus, a.error, a.duration_ms AS durationMs,
                   a.next_attempt_at AS nextAttemptAt
            FROM agent_delivery_attempt a JOIN agent_delivery d ON d.id = a.delivery_id
            WHERE d.session_id = ANY(@sessionIds)
            ORDER BY a.at, d.seq, a.attempt
            """,
            new { sessionIds = sessionIds.ToArray() }, cancellationToken: cancellationToken));
        return rows.ToLookup(r => r.SessionId, ToDto);
    }

    private AgentDeliveryAttemptDto ToDto(Row row)
        => new(
            row.Action,
            row.Attempt,
            row.At,
            DisplayDate.Relative(row.At, time),
            row.HttpStatus,
            row.Error,
            row.DurationMs,
            row.NextAttemptAt,
            row.NextAttemptAt is { } next ? DisplayDate.Relative(next, time) : null);

    private sealed record Row(
        Guid SessionId, string Action, int Attempt, DateTimeOffset At, int? HttpStatus, string? Error, int DurationMs, DateTimeOffset? NextAttemptAt);
}
