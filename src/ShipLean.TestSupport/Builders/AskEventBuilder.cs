using Npgsql;
using ShipLean.Application.Features.Asks.Sql;
using ShipLean.TestSupport.Asks;

namespace ShipLean.TestSupport.Builders;

// Arranges one ask_event row through the real AskEventWriter. Call ForDecision or ForQuestion (the table needs exactly one).
// Defaults: kind raised, by Joel through "Claude Code (test)", now. Returns the event's id: events have no number.
public sealed class AskEventBuilder(Guid workItemId)
{
    private readonly Guid _id = Guid.NewGuid();
    private Guid? _decisionId;
    private Guid? _questionId;
    private string _kind = "raised";
    private string? _from;
    private string? _to;
    private string? _toKind;
    private string? _note;
    private Guid? _by = SeedIds.Users.Joel;
    private string? _agent = AskDefaults.AgentName;
    private DateTimeOffset _at = AskDefaults.Now();
    private bool _fromDescription;

    public AskEventBuilder ForDecision(Guid decisionId)
    {
        _decisionId = decisionId;
        return this;
    }

    public AskEventBuilder ForQuestion(Guid questionId)
    {
        _questionId = questionId;
        return this;
    }

    // raised | edited | confirmed | changed | withdrawn | asked | answered
    public AskEventBuilder Kind(string kind)
    {
        _kind = kind;
        return this;
    }

    public AskEventBuilder From(string? from)
    {
        _from = from;
        return this;
    }

    public AskEventBuilder To(string? to)
    {
        _to = to;
        return this;
    }

    // recommended | alternative | own
    public AskEventBuilder ToKind(string? toKind)
    {
        _toKind = toKind;
        return this;
    }

    public AskEventBuilder Note(string? note)
    {
        _note = note;
        return this;
    }

    public AskEventBuilder By(Guid? user, string? agent)
    {
        _by = user;
        _agent = agent;
        return this;
    }

    public AskEventBuilder At(DateTimeOffset at)
    {
        _at = at;
        return this;
    }

    public AskEventBuilder FromDescription()
    {
        _fromDescription = true;
        return this;
    }

    public async Task<Guid> BuildAsync(NpgsqlConnection connection)
    {
        await using var transaction = await connection.BeginTransactionAsync();
        await AskEventWriter.InsertAsync(
            transaction,
            new AskEventRow(_id, workItemId, _decisionId, _questionId, _kind, _from, _to, _toKind, _note, _by, _agent, _at, _fromDescription));
        await transaction.CommitAsync();
        return _id;
    }
}
