using Npgsql;
using ShipLean.Application.Features.Asks.Sql;

namespace ShipLean.TestSupport.Builders;

// Tags an existing comment as one the asks section posted, through the real AskTagWriter. Comment is required, and Decision or
// Question (the table needs exactly one). Kind defaults to about. Returns the comment id, the tag's key.
public sealed class AskTagBuilder
{
    private Guid? _commentId;
    private string _kind = "about";
    private Guid? _decisionId;
    private Guid? _questionId;

    public AskTagBuilder Comment(Guid commentId)
    {
        _commentId = commentId;
        return this;
    }

    // answer | changed | about
    public AskTagBuilder Kind(string kind)
    {
        _kind = kind;
        return this;
    }

    public AskTagBuilder Decision(Guid decisionId)
    {
        _decisionId = decisionId;
        return this;
    }

    public AskTagBuilder Question(Guid questionId)
    {
        _questionId = questionId;
        return this;
    }

    public async Task<Guid> BuildAsync(NpgsqlConnection connection)
    {
        var commentId = _commentId ?? throw new InvalidOperationException("AskTagBuilder needs a comment: call Comment(id).");
        await using var transaction = await connection.BeginTransactionAsync();
        await AskTagWriter.InsertAsync(transaction, commentId, _kind, _decisionId, _questionId);
        await transaction.CommitAsync();
        return commentId;
    }
}
