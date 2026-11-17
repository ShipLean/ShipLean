using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;

namespace ShipLean.TestSupport.Builders;

// Arranges agent deliveries and their attempts with parameterised SQL, so a test can seed a delivery log without the deliverer.
// A delivery's body is an empty object: never put a real body here unless the test is about where bodies go.
public static class AgentDeliveryAttemptRows
{
    // created | prompted; status pending | sent | failed.
    public static async Task<Guid> InsertDeliveryAsync(NpgsqlConnection connection, Guid sessionId, int seq = 1, string action = "created", string status = "failed")
    {
        var id = Guid.CreateVersion7();
        await connection.ExecuteAsync(
            """
            INSERT INTO agent_delivery (id, session_id, seq, action, body, next_attempt_at, status, created_at)
            VALUES (@id, @sessionId, @seq, @action, '{}'::jsonb, now(), @status, now())
            """,
            new { id, sessionId, seq, action, status });
        return id;
    }

    public static async Task<Guid> InsertAsync(NpgsqlConnection connection, AgentDeliveryAttemptCreate attempt)
    {
        var id = Guid.CreateVersion7();
        await connection.ExecuteAsync(
            """
            INSERT INTO agent_delivery_attempt (id, delivery_id, connection_id, attempt, at, http_status, error, duration_ms, next_attempt_at)
            VALUES (@id, @deliveryId, @connectionId, @attempt, @at, @httpStatus, @error, @durationMs, @nextAttemptAt)
            """,
            new
            {
                id,
                attempt.DeliveryId,
                attempt.ConnectionId,
                attempt.Attempt,
                at = attempt.At.ToUniversalTime(),
                attempt.HttpStatus,
                attempt.Error,
                attempt.DurationMs,
                nextAttemptAt = attempt.NextAttemptAt?.ToUniversalTime(),
            });
        return id;
    }
}
