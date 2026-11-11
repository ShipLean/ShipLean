using Dapper;
using Npgsql;
using ShipLean.TestSupport.Asks;

namespace ShipLean.TestSupport.Builders;

// Arranges one question on a work item with parameterised SQL. Defaults: the next free number, open, asked now by Joel through
// "Claude Code (test)". Answered and Withdrawn also set the state; State afterwards overrides it.
public sealed class QuestionBuilder(Guid workItemId)
{
    private readonly Guid _id = Guid.NewGuid();
    private int? _number;
    private string _text = "Arranged question?";
    private string _state = "open";
    private Guid _askedBy = SeedIds.Users.Joel;
    private string? _askedAgent = AskDefaults.AgentName;
    private DateTimeOffset _askedAt = AskDefaults.Now();
    private Guid? _answerCommentId;
    private Guid? _answeredBy;
    private DateTimeOffset? _answeredAt;
    private Guid? _withdrawnBy;
    private DateTimeOffset? _withdrawnAt;
    private string[] _options = [];

    public QuestionBuilder Number(int number)
    {
        _number = number;
        return this;
    }

    public QuestionBuilder Text(string text)
    {
        _text = text;
        return this;
    }

    // open | answered | withdrawn
    public QuestionBuilder State(string state)
    {
        _state = state;
        return this;
    }

    public QuestionBuilder AskedBy(Guid user, string? agent, DateTimeOffset at)
    {
        _askedBy = user;
        _askedAgent = agent;
        _askedAt = at;
        return this;
    }

    public QuestionBuilder Answered(Guid commentId, Guid user, DateTimeOffset at)
    {
        _state = "answered";
        _answerCommentId = commentId;
        _answeredBy = user;
        _answeredAt = at;
        return this;
    }

    public QuestionBuilder Withdrawn(Guid user, DateTimeOffset at)
    {
        _state = "withdrawn";
        _withdrawnBy = user;
        _withdrawnAt = at;
        return this;
    }

    // The answers the asker offers (question.options), in order. At most 6; the schema refuses more.
    public QuestionBuilder WithOptions(params string[] options)
    {
        _options = options;
        return this;
    }

    public async Task<(Guid Id, int Number)> BuildAsync(NpgsqlConnection connection)
    {
        var number = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO question (id, work_item_id, number, text, state, created_by_user_id, created_agent_name, created_at,
                                  answer_comment_id, answered_by_user_id, answered_at, withdrawn_by_user_id, withdrawn_at, options)
            SELECT @id, @workItemId, coalesce(@number::int, (SELECT coalesce(max(number), 0) + 1 FROM question WHERE work_item_id = @workItemId)),
                   @text, @state, @askedBy, @askedAgent, @askedAt, @answerCommentId, @answeredBy, @answeredAt, @withdrawnBy, @withdrawnAt,
                   to_jsonb(@options::text[])
            RETURNING number
            """,
            new
            {
                id = _id, workItemId, number = _number, text = _text, state = _state,
                askedBy = _askedBy, askedAgent = _askedAgent, askedAt = _askedAt.ToUniversalTime(),
                answerCommentId = _answerCommentId, answeredBy = _answeredBy, answeredAt = _answeredAt?.ToUniversalTime(),
                withdrawnBy = _withdrawnBy, withdrawnAt = _withdrawnAt?.ToUniversalTime(), options = _options,
            });
        return (_id, number);
    }
}
