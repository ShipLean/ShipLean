using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Contracts.Agents;
using ShipLean.Core;

namespace ShipLean.Application.Features.AgentDispatch;

// agent_delivery_attempt. ForSessionsAsync reads a session's attempts through their delivery, oldest first (by time, then the
// delivery's seq, then the attempt number), with server labels.
public sealed class AgentDeliveryAttempts(NpgsqlDataSource dataSource, TimeProvider time, ILogger<AgentDeliveryAttempts> logger) : IAgentDeliveryAttempts
{
    // One row per send. A resend after a failed sent-mark reuses the attempt number, so its row is already there and the insert does nothing.
    // The delivery is already decided by the time this runs, so a database error is logged and never thrown to the sender (only a cancel is).
    public async Task RecordAsync(AgentDeliveryAttemptCreate attempt, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                """
                INSERT INTO agent_delivery_attempt (id, delivery_id, connection_id, attempt, at, http_status, error, duration_ms, next_attempt_at)
                VALUES (@id, @deliveryId, @connectionId, @attempt, @at, @httpStatus, @error, @durationMs, @nextAttemptAt)
                ON CONFLICT (delivery_id, attempt) DO NOTHING
                """,
                new
                {
                    id = Guid.CreateVersion7(),
                    attempt.DeliveryId,
                    attempt.ConnectionId,
                    attempt.Attempt,
                    at = attempt.At.ToUniversalTime(),
                    attempt.HttpStatus,
                    attempt.Error,
                    attempt.DurationMs,
                    nextAttemptAt = attempt.NextAttemptAt?.ToUniversalTime(),
                },
                cancellationToken: cancellationToken));
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            // The error text is not logged: only ids and the attempt number.
            logger.LogWarning(failed, "Could not record attempt {Attempt} of agent delivery {DeliveryId}", attempt.Attempt, attempt.DeliveryId);
        }
    }

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
