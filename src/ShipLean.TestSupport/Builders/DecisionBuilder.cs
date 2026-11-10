using Dapper;
using Npgsql;
using ShipLean.TestSupport.Asks;

namespace ShipLean.TestSupport.Builders;

// Arranges one decision on a work item with parameterised SQL. Defaults: the next free number, open, the recommended answer
// standing, no alternatives, raised now by Joel through "Claude Code (test)". The table's checks still apply: an open decision's
// answer must be the recommended one, and only an alternative answer carries an index.
public sealed class DecisionBuilder(Guid workItemId)
{
    private readonly Guid _id = Guid.NewGuid();
    private int? _number;
    private string _question = "Arranged decision?";
    private string _recommended = "The recommended answer";
    private string[] _alternatives = [];
    private string _state = "open";
    private string? _answerKind;
    private string? _answerText;
    private int? _alternativeIndex;
    private Guid? _answeredBy;
    private string? _answeredAgent;
    private DateTimeOffset? _answeredAt;
    private Guid _raisedBy = SeedIds.Users.Joel;
    private string? _raisedAgent = AskDefaults.AgentName;
    private DateTimeOffset _raisedAt = AskDefaults.Now();
    private bool _fromDescription;

    public DecisionBuilder Number(int number)
    {
        _number = number;
        return this;
    }

    public DecisionBuilder Question(string question)
    {
        _question = question;
        return this;
    }

    public DecisionBuilder Recommended(string recommended)
    {
        _recommended = recommended;
        return this;
    }

    public DecisionBuilder Alternatives(params string[] alternatives)
    {
        _alternatives = alternatives;
        return this;
    }

    // open | confirmed | changed | withdrawn
    public DecisionBuilder State(string state)
    {
        _state = state;
        return this;
    }

    // kind: recommended | alternative | own. The text becomes current_answer; index is set for an alternative only.
    public DecisionBuilder Answer(string kind, string text, int? index)
    {
        _answerKind = kind;
        _answerText = text;
        _alternativeIndex = index;
        return this;
    }

    public DecisionBuilder AnsweredBy(Guid user, DateTimeOffset at, string? agent = null)
    {
        _answeredBy = user;
        _answeredAt = at;
        _answeredAgent = agent;
        return this;
    }

    public DecisionBuilder RaisedBy(Guid user, string? agent, DateTimeOffset at)
    {
        _raisedBy = user;
        _raisedAgent = agent;
        _raisedAt = at;
        return this;
    }

    public DecisionBuilder FromDescription()
    {
        _fromDescription = true;
        return this;
    }

    public async Task<(Guid Id, int Number)> BuildAsync(NpgsqlConnection connection)
    {
        var number = await connection.ExecuteScalarAsync<int>(
            """
            INSERT INTO decision (id, work_item_id, number, question, recommended, alternatives, current_answer, answer_kind,
                                  alternative_index, state, created_by_user_id, created_agent_name, created_at,
                                  answered_by_user_id, answered_agent_name, answered_at, from_description, updated_at)
            SELECT @id, @workItemId, coalesce(@number::int, (SELECT coalesce(max(number), 0) + 1 FROM decision WHERE work_item_id = @workItemId)),
                   @question, @recommended, to_jsonb(@alternatives::text[]), coalesce(@answerText, @recommended),
                   coalesce(@answerKind, 'recommended'), @alternativeIndex, @state, @raisedBy, @raisedAgent, @raisedAt,
                   @answeredBy, @answeredAgent, @answeredAt, @fromDescription, coalesce(@answeredAt, @raisedAt)
            RETURNING number
            """,
            new
            {
                id = _id, workItemId, number = _number, question = _question, recommended = _recommended, alternatives = _alternatives,
                answerText = _answerText, answerKind = _answerKind, alternativeIndex = _alternativeIndex, state = _state,
                raisedBy = _raisedBy, raisedAgent = _raisedAgent, raisedAt = _raisedAt.ToUniversalTime(),
                answeredBy = _answeredBy, answeredAgent = _answeredAgent, answeredAt = _answeredAt?.ToUniversalTime(),
                fromDescription = _fromDescription,
            });
        return (_id, number);
    }
}
