using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Builders;

// Arranges one agent session with parameterised SQL and returns its id, so a test can seed an ended session without the
// writers. Defaults: a completed session by Joel, started now, ended now, no end reason, detail or run log. ended_at is set for
// completed, failed and canceled and left null for a live or stale session. (AgentSessionRows in WebApi.Tests is the access
// sweep, not this.) InsertConnectionAsync arranges a bare agent connection to start sessions on.
public sealed class AgentSessionSeeds(Guid workItemId, Guid connectionId)
{
    private Guid _id = Guid.CreateVersion7();
    private string _state = "completed";
    private string? _endReason;
    private string? _endDetail;
    private Guid? _runLogAttachmentId;
    private Guid _createdBy = SeedIds.Users.Joel;
    private DateTimeOffset? _createdAt;

    public AgentSessionSeeds WithId(Guid id)
    {
        _id = id;
        return this;
    }

    // submitted | working | inputRequired | completed | failed | canceled | stale
    public AgentSessionSeeds WithState(string state)
    {
        _state = state;
        return this;
    }

    // AgentEndReasons; null for a live session.
    public AgentSessionSeeds WithEndReason(string? endReason)
    {
        _endReason = endReason;
        return this;
    }

    public AgentSessionSeeds WithEndDetail(string? endDetail)
    {
        _endDetail = endDetail;
        return this;
    }

    public AgentSessionSeeds WithRunLogAttachment(Guid? attachmentId)
    {
        _runLogAttachmentId = attachmentId;
        return this;
    }

    public AgentSessionSeeds WithCreatedBy(Guid userId)
    {
        _createdBy = userId;
        return this;
    }

    public AgentSessionSeeds WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public async Task<Guid> InsertAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO agent_session (id, work_item_id, connection_id, state, repo_url, base_branch, last_activity_at, created_by, created_at, ended_at,
                                       end_reason, end_detail, run_log_attachment_id)
            VALUES (@id, @workItemId, @connectionId, @state, 'https://github.com/ShipLean/ShipLean.git', 'main', @createdAt, @createdBy, @createdAt,
                    CASE WHEN @state IN ('completed', 'failed', 'canceled') THEN @createdAt END, @endReason, @endDetail, @runLogAttachmentId)
            """,
            new
            {
                id = _id,
                workItemId,
                connectionId,
                state = _state,
                createdAt = (_createdAt ?? TimeProvider.System.GetUtcNow()).ToUniversalTime(),
                createdBy = _createdBy,
                endReason = _endReason,
                endDetail = _endDetail,
                runLogAttachmentId = _runLogAttachmentId,
            });
        return _id;
    }

    // A linear-compatible connection row with a unique name and no usable secret: enough to hang sessions and deliveries on.
    public static async Task<Guid> InsertConnectionAsync(NpgsqlConnection connection)
    {
        var id = Guid.CreateVersion7();
        await connection.ExecuteAsync(
            """
            INSERT INTO agent_connection (id, type, name, receiver_url, signing_secret_protected, signing_secret_last4, callback_token_hash, callback_token_last4, created_at, updated_at)
            VALUES (@id, 'linear-compatible', @name, 'https://relay.example.com/hook', 'x', 'xxxx', @hash, 'xxxx', now(), now())
            """,
            new { id, name = $"Seeded relay {id:N}", hash = id.ToByteArray() });
        return id;
    }
}
