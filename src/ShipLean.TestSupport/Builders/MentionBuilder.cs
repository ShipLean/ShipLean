using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Builders;

// Arranges one comment_mention row with parameterised SQL and returns its id: the person userId named in commentId on workItemId.
// The default is an open mention made now, not closed and not replied to. The comment and the item must exist.
public sealed class MentionBuilder(Guid commentId, Guid workItemId, Guid userId)
{
    private Guid _id = Guid.NewGuid();
    private string _state = "open";
    private DateTimeOffset? _createdAt;
    private DateTimeOffset? _closedAt;
    private Guid? _replyCommentId;

    public MentionBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    // open | seen | replied | withdrawn
    public MentionBuilder WithState(string state)
    {
        _state = state;
        return this;
    }

    public MentionBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public MentionBuilder WithClosedAt(DateTimeOffset closedAt)
    {
        _closedAt = closedAt;
        return this;
    }

    public MentionBuilder WithReply(Guid replyCommentId)
    {
        _replyCommentId = replyCommentId;
        return this;
    }

    public async Task<Guid> InsertAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO comment_mention (id, comment_id, work_item_id, user_id, state, created_at, closed_at, reply_comment_id)
            VALUES (@id, @commentId, @workItemId, @userId, @state, coalesce(@createdAt::timestamptz, now()), @closedAt::timestamptz,
                    @replyCommentId::uuid)
            """,
            new
            {
                id = _id, commentId, workItemId, userId, state = _state, createdAt = _createdAt?.ToUniversalTime(),
                closedAt = _closedAt?.ToUniversalTime(), replyCommentId = _replyCommentId,
            });
        return _id;
    }
}
