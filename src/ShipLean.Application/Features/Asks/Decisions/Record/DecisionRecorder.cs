using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Asks.Sql;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.Asks.Decisions.Record;

// IDecisionRecorder over the decision and ask_event tables. One transaction under AskNumbers' per-item lock: records with a
// number go first, in the order given, then the numberless ones take max + 1. Any refusal rolls the whole call back.
// The actor is ICurrentUser, stamped with AgentStamp; a Settled record is answered by its ByUserId on its day, from the description.
public sealed class DecisionRecorder(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    ICurrentAgent currentAgent,
    TimeProvider clock,
    IProjectEvents events) : IDecisionRecorder
{
    private const int MaxText = 500;
    private const int MaxAlternatives = 6;
    private const string Field = "decisions";

    public async Task<RecordedDecisions> RecordAsync(Guid workItemId, IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken = default)
    {
        var cleaned = Validate(records);
        var actor = new Actor(currentUser.Id, AgentStamp.Of(currentAgent), clock.GetUtcNow());

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AskNumbers.LockAsync(transaction, workItemId, cancellationToken);
        var projectId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT project_id FROM work_item WHERE id = @workItemId", new { workItemId }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("That work item does not exist.");
        await RequireSettlersAsync(connection, transaction, projectId, cleaned, cancellationToken);

        var created = new List<int>();
        var updated = new List<int>();
        foreach (var record in cleaned.Where(r => r.Number is not null).Concat(cleaned.Where(r => r.Number is null)))
        {
            var existing = record.Number is { } number
                ? await connection.QuerySingleOrDefaultAsync<ExistingDecision>(new CommandDefinition(
                    """
                    SELECT id AS Id, question AS Question, recommended AS Recommended, alternatives::text AS Alternatives, state AS State
                    FROM decision WHERE work_item_id = @workItemId AND number = @number
                    """,
                    new { workItemId, number }, transaction, cancellationToken: cancellationToken))
                : null;
            if (existing is null)
            {
                var next = record.Number ?? await AskNumbers.NextDecisionAsync(transaction, workItemId, cancellationToken);
                await CreateAsync(transaction, workItemId, next, record, actor, cancellationToken);
                created.Add(next);
            }
            else if (existing.State != "open")
            {
                throw ApiException.Validation($"D{record.Number} is {existing.State}: only an open decision can be edited.", Field);
            }
            else if (record.Settled is not null)
            {
                throw ApiException.Validation($"D{record.Number} is open: a settled decision can only be created, not edited.", Field);
            }
            else if (await EditAsync(transaction, workItemId, existing, record, actor, cancellationToken))
            {
                updated.Add(record.Number!.Value);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        if (created.Count > 0 || updated.Count > 0)
        {
            await events.PublishAsync(new AsksChanged(projectId, workItemId));
        }

        return new RecordedDecisions(created, updated);
    }

    private static async Task CreateAsync(
        NpgsqlTransaction transaction, Guid workItemId, int number, DecisionRecord record, Actor actor, CancellationToken cancellationToken)
    {
        var id = Guid.NewGuid();
        var answer = Answer.Of(record);
        var answeredAt = record.Settled is { } settled ? new DateTimeOffset(settled.On.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : (DateTimeOffset?)null;
        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO decision (id, work_item_id, number, question, recommended, alternatives, current_answer, answer_kind, alternative_index,
                                  state, created_by_user_id, created_agent_name, created_at, answered_by_user_id, answered_at,
                                  from_description, updated_at)
            VALUES (@id, @workItemId, @number, @question, @recommended, @alternatives::jsonb, @currentAnswer, @answerKind, @alternativeIndex,
                    @state, @userId, @agentName, @now, @answeredBy, @answeredAt, @fromDescription, @now)
            """,
            new
            {
                id,
                workItemId,
                number,
                question = record.Question,
                recommended = record.Recommended,
                alternatives = JsonSerializer.Serialize(record.Alternatives),
                currentAnswer = answer.Text,
                answerKind = answer.Kind,
                alternativeIndex = answer.Index,
                state = answer.State,
                userId = actor.UserId,
                agentName = actor.AgentName,
                now = actor.Now,
                answeredBy = record.Settled?.ByUserId,
                answeredAt,
                fromDescription = record.Settled is not null,
            },
            transaction,
            cancellationToken: cancellationToken));

        await AskEventWriter.InsertAsync(transaction, new AskEventRow(
            Guid.NewGuid(), workItemId, id, null, "raised", null, record.Recommended, "recommended", null,
            actor.UserId, actor.AgentName, actor.Now, false), cancellationToken);
        if (record.Settled is { } by)
        {
            // The answer in force; a change also says what it replaced.
            await AskEventWriter.InsertAsync(transaction, new AskEventRow(
                Guid.NewGuid(), workItemId, id, null, answer.State, answer.State == "changed" ? record.Recommended : null, answer.Text, answer.Kind, null,
                by.ByUserId, null, answeredAt!.Value, true), cancellationToken);
        }
    }

    // False when the record matches the stored decision: nothing is written and no event is added.
    private static async Task<bool> EditAsync(
        NpgsqlTransaction transaction, Guid workItemId, ExistingDecision existing, DecisionRecord record, Actor actor, CancellationToken cancellationToken)
    {
        var alternatives = JsonSerializer.Serialize(record.Alternatives);
        var stored = JsonSerializer.Deserialize<string[]>(existing.Alternatives)!;
        if (existing.Question == record.Question && existing.Recommended == record.Recommended && stored.SequenceEqual(record.Alternatives))
        {
            return false;
        }

        // Open means the recommended answer stands, so it moves with the recommendation in the same statement (ck_decision_open_answer).
        await transaction.Connection!.ExecuteAsync(new CommandDefinition(
            """
            UPDATE decision SET question = @question, recommended = @recommended, current_answer = @recommended,
                                alternatives = @alternatives::jsonb, updated_at = @now
            WHERE id = @id AND state = 'open'
            """,
            new { id = existing.Id, question = record.Question, recommended = record.Recommended, alternatives, now = actor.Now },
            transaction,
            cancellationToken: cancellationToken));
        await AskEventWriter.InsertAsync(transaction, new AskEventRow(
            Guid.NewGuid(), workItemId, existing.Id, null, "edited", existing.Question, record.Question, null, null,
            actor.UserId, actor.AgentName, actor.Now, false), cancellationToken);
        return true;
    }

    private static async Task RequireSettlersAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid projectId, IReadOnlyList<DecisionRecord> records, CancellationToken cancellationToken)
    {
        foreach (var userId in records.Select(r => r.Settled?.ByUserId).OfType<Guid>().Distinct())
        {
            var member = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT EXISTS (SELECT 1 FROM project_member WHERE project_id = @projectId AND user_id = @userId AND archived_at IS NULL)",
                new { projectId, userId }, transaction, cancellationToken: cancellationToken));
            if (!member)
            {
                throw ApiException.Validation("A settled decision must be answered by a member of the project.", Field);
            }
        }
    }

    // Checks every record before anything is read or written, and returns them trimmed.
    private static List<DecisionRecord> Validate(IReadOnlyList<DecisionRecord>? records)
    {
        if (records is null || records.Count == 0)
        {
            throw ApiException.Validation("Give at least one decision.", Field);
        }

        var cleaned = new List<DecisionRecord>(records.Count);
        foreach (var record in records)
        {
            if (record is null)
            {
                throw ApiException.Validation("A decision cannot be empty.", Field);
            }

            var question = Required(record.Question, "question");
            var recommended = Required(record.Recommended, "recommended answer");
            if (record.Number is <= 0)
            {
                throw ApiException.Validation("A decision number must be greater than 0.", Field);
            }

            var alternatives = (record.Alternatives ?? []).Select(a => Required(a, "alternative")).ToList();
            if (alternatives.Count > MaxAlternatives)
            {
                throw ApiException.Validation($"A decision has at most {MaxAlternatives} alternatives.", Field);
            }

            if (alternatives.Contains(recommended, StringComparer.Ordinal))
            {
                throw ApiException.Validation("An alternative must differ from the recommended answer.", Field);
            }

            var settled = record.Settled is { } s ? ValidSettled(s, alternatives) : null;
            cleaned.Add(new DecisionRecord(record.Number, question, recommended, alternatives, settled));
        }

        if (cleaned.Where(r => r.Number is not null).GroupBy(r => r.Number).Any(g => g.Count() > 1))
        {
            throw ApiException.Validation("Each decision number can appear once in a call.", Field);
        }

        return cleaned;
    }

    private static SettledAnswer ValidSettled(SettledAnswer settled, IReadOnlyList<string> alternatives) => settled.Kind switch
    {
        AnswerKind.Recommended => settled with { AlternativeIndex = null, Text = null },
        AnswerKind.Alternative when settled.AlternativeIndex is { } index && index >= 0 && index < alternatives.Count
            => settled with { Text = null },
        AnswerKind.Alternative => throw ApiException.Validation("A settled alternative needs the index of one of the alternatives.", Field),
        AnswerKind.Own => settled with { AlternativeIndex = null, Text = Required(settled.Text, "own answer") },
        _ => throw ApiException.Validation("A settled decision needs an answer kind.", Field),
    };

    private static string Required(string? text, string what)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            throw ApiException.Validation($"A decision needs its {what}.", Field);
        }

        var trimmed = text.Trim();
        return trimmed.Length > MaxText
            ? throw ApiException.Validation($"A decision's {what} is at most {MaxText} characters.", Field)
            : trimmed;
    }

    private sealed record Actor(Guid UserId, string? AgentName, DateTimeOffset Now);

    private sealed record ExistingDecision(Guid Id, string Question, string Recommended, string Alternatives, string State);

    // The answer a new decision holds: open (the recommended one), or the settled answer, confirmed or changed.
    private sealed record Answer(string Text, string Kind, int? Index, string State)
    {
        public static Answer Of(DecisionRecord record) => record.Settled switch
        {
            null => new(record.Recommended, "recommended", null, "open"),
            { Kind: AnswerKind.Recommended } => new(record.Recommended, "recommended", null, "confirmed"),
            { Kind: AnswerKind.Alternative, AlternativeIndex: { } index } => new(record.Alternatives[index], "alternative", index, "changed"),
            { Text: { } text } => new(text, "own", null, "changed"),
            _ => throw new InvalidOperationException("Validate leaves no other settled answer."),
        };
    }
}
