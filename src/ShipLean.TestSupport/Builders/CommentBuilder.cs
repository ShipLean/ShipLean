using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Builders;

// Arranges one comment on a work item with parameterised SQL and returns its id. The default is a comment by no one,
// written now. The real comment store is a later slice's; this inserts the row directly.
public sealed class CommentBuilder(Guid workItemId)
{
    private Guid _id = Guid.NewGuid();
    private Guid? _authorId;
    private string _body = "Arranged comment";
    private DateTimeOffset? _createdAt;
    private bool _archived;

    public CommentBuilder WithId(Guid id)
    {
        _id = id;
        return this;
    }

    public CommentBuilder WithAuthor(Guid userId)
    {
        _authorId = userId;
        return this;
    }

    public CommentBuilder WithBody(string body)
    {
        _body = body;
        return this;
    }

    public CommentBuilder WithCreatedAt(DateTimeOffset createdAt)
    {
        _createdAt = createdAt;
        return this;
    }

    public CommentBuilder Archived()
    {
        _archived = true;
        return this;
    }

    public async Task<Guid> InsertAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO comment (id, work_item_id, author_id, body, created_at, updated_at, archived_at)
            VALUES (@id, @workItemId, @authorId::uuid, @body, coalesce(@createdAt::timestamptz, now()), coalesce(@createdAt::timestamptz, now()),
                    CASE WHEN @archived THEN now() END)
            """,
            new { id = _id, workItemId, authorId = _authorId, body = _body, createdAt = _createdAt?.ToUniversalTime(), archived = _archived });
        return _id;
    }
}
