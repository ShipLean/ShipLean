using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Core;

namespace ShipLean.Application.Features.Asks.Reads;

// Reads a work item's decisions, questions and history from the asks tables, and for a Feature the Stories and Bugs under it
// with open asks. Trusts its caller to have checked access.
public sealed class ItemAsks(NpgsqlDataSource dataSource, ICurrentUser currentUser, MarkdownRenderer renderer, TimeProvider clock) : IItemAsks
{
    public async Task<ItemAsksDto> GetAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var item = await connection.QuerySingleOrDefaultAsync<ItemRow>(new CommandDefinition(
            "SELECT type, assigned_to AS assignedTo, created_by AS createdBy FROM work_item WHERE id = @workItemId",
            new { workItemId }, cancellationToken: cancellationToken));
        if (item is null)
        {
            throw ApiException.NotFound("That work item does not exist.");
        }

        // The answer comment is joined with no archived filter: an archived comment still shows as the answer.
        var questions = (await connection.QueryAsync<QuestionRow>(new CommandDefinition(
            """
            SELECT q.id, q.number, q.text, q.options::text AS options, q.state, q.created_by_user_id AS askedBy, q.created_agent_name AS agentName, q.created_at AS askedAt,
                   q.withdrawn_at AS withdrawnAt, q.answer_comment_id AS answerCommentId,
                   c.body AS answerBody, c.author_id AS answerAuthor, c.agent_name AS answerAgentName, c.created_at AS answerAt
            FROM question q LEFT JOIN comment c ON c.id = q.answer_comment_id
            WHERE q.work_item_id = @workItemId
            ORDER BY (q.state = 'open') DESC, q.number
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var decisions = (await connection.QueryAsync<DecisionRow>(new CommandDefinition(
            """
            SELECT id, number, question, recommended, alternatives::text AS alternatives, current_answer AS currentAnswer,
                   answer_kind AS answerKind, alternative_index AS alternativeIndex, state, created_by_user_id AS raisedBy,
                   created_agent_name AS raisedAgentName, created_at AS raisedAt, answered_by_user_id AS answeredBy,
                   answered_agent_name AS answeredAgentName, answered_at AS answeredAt, from_description AS fromDescription
            FROM decision WHERE work_item_id = @workItemId ORDER BY number
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var events = (await connection.QueryAsync<EventRow>(new CommandDefinition(
            """
            SELECT decision_id AS decisionId, kind, from_answer AS fromAnswer, to_answer AS toAnswer, to_kind AS toKind, note,
                   by_user_id AS byUser, agent_name AS agentName, at, from_description AS fromDescription
            FROM ask_event WHERE work_item_id = @workItemId AND decision_id IS NOT NULL ORDER BY at, id
            """,
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var children = item.Type == "feature"
            ? (await connection.QueryAsync<ChildRow>(new CommandDefinition(
                """
                SELECT w.id, w.key, w.type, w.title, w.assigned_to IS NULL AS ownerIsCreator, coalesce(w.assigned_to, w.created_by) AS owner,
                       (SELECT count(*) FROM question q WHERE q.work_item_id = w.id AND q.state = 'open')::int AS openQuestions,
                       (SELECT count(*) FROM decision d WHERE d.work_item_id = w.id AND d.state = 'open')::int AS openDecisions
                FROM work_item w JOIN state s ON s.id = w.state_id
                WHERE w.parent_id = @workItemId AND w.type IN ('story', 'bug') AND s.category <> 'removed'
                  AND (EXISTS (SELECT 1 FROM question q WHERE q.work_item_id = w.id AND q.state = 'open')
                       OR EXISTS (SELECT 1 FROM decision d WHERE d.work_item_id = w.id AND d.state = 'open'))
                ORDER BY w.backlog_order, w.number
                """,
                new { workItemId }, cancellationToken: cancellationToken))).ToList()
            : [];

        var owner = item.AssignedTo ?? item.CreatedBy;
        var userIds = new HashSet<Guid>();
        void Add(params Guid?[] ids) => userIds.UnionWith(ids.OfType<Guid>());

        Add(owner);
        questions.ForEach(q => Add(q.AskedBy, q.AnswerAuthor));
        decisions.ForEach(d => Add(d.RaisedBy, d.AnsweredBy));
        events.ForEach(e => Add(e.ByUser));
        children.ForEach(c => Add(c.Owner));
        var users = (await connection.QueryAsync<UserRef>(new CommandDefinition(
            $"SELECT {UserRefs.Columns} FROM app_user u WHERE u.id = ANY(@ids)",
            new { ids = userIds.ToArray() }, cancellationToken: cancellationToken))).ToDictionary(u => u.Id);
        UserRef? User(Guid? id) => id is { } value ? users.GetValueOrDefault(value) : null;

        var questionDtos = questions.Select(q => new QuestionDto(
            q.Id, q.Number, $"Q{q.Number}", q.Text, JsonSerializer.Deserialize<string[]>(q.Options)!, Enum.Parse<QuestionState>(q.State, true), users[q.AskedBy], q.AgentName,
            q.AskedAt, DisplayDate.Relative(q.AskedAt, clock),
            q.AnswerCommentId is { } commentId && q.AnswerAt is { } answerAt
                ? Answer(commentId, q.AnswerBody!, User(q.AnswerAuthor), q.AnswerAgentName, answerAt)
                : null,
            q.WithdrawnAt, q.WithdrawnAt is { } w ? DisplayDate.Relative(w, clock) : null)).ToList();
        var eventsByDecision = events.ToLookup(e => e.DecisionId!.Value);
        var decisionDtos = decisions.Select(d => new DecisionDto(
            d.Id, d.Number, $"D{d.Number}", d.Question, d.Recommended, JsonSerializer.Deserialize<string[]>(d.Alternatives)!, d.CurrentAnswer,
            Enum.Parse<AnswerKind>(d.AnswerKind, true), d.AlternativeIndex, Enum.Parse<DecisionState>(d.State, true), users[d.RaisedBy],
            d.RaisedAgentName, d.RaisedAt, DisplayDate.Relative(d.RaisedAt, clock), User(d.AnsweredBy), d.AnsweredAgentName, d.AnsweredAt,
            d.AnsweredAt is { } at ? DisplayDate.Relative(at, clock) : null, d.FromDescription,
            [.. eventsByDecision[d.Id].Select(e => new DecisionEventDto(
                Enum.Parse<AskEventKind>(e.Kind, true), e.FromAnswer, e.ToAnswer,
                e.ToKind is { } kind ? Enum.Parse<AnswerKind>(kind, true) : null, e.Note, User(e.ByUser), e.AgentName,
                e.At, DisplayDate.Relative(e.At, clock), e.FromDescription))])).ToList();

        return new ItemAsksDto(
            workItemId, User(owner), item.AssignedTo is null, owner == currentUser.Id,
            questionDtos.Count(q => q.State == QuestionState.Open), decisionDtos.Count(d => d.State == DecisionState.Open),
            questionDtos, decisionDtos,
            [.. children.Select(c => new WaitingChildDto(c.Id, c.Key, c.Type, c.Title, User(c.Owner), c.OwnerIsCreator, c.OpenQuestions, c.OpenDecisions))]);
    }

    private QuestionAnswerDto Answer(Guid commentId, string body, UserRef? by, string? agentName, DateTimeOffset at)
        => new(commentId, body, renderer.Render(body).Html, by, agentName, at, DisplayDate.Relative(at, clock));

    private sealed record ItemRow(string Type, Guid? AssignedTo, Guid? CreatedBy);

    private sealed record QuestionRow(
        Guid Id, int Number, string Text, string Options, string State, Guid AskedBy, string? AgentName, DateTimeOffset AskedAt, DateTimeOffset? WithdrawnAt,
        Guid? AnswerCommentId, string? AnswerBody, Guid? AnswerAuthor, string? AnswerAgentName, DateTimeOffset? AnswerAt);

    private sealed record DecisionRow(
        Guid Id, int Number, string Question, string Recommended, string Alternatives, string CurrentAnswer, string AnswerKind,
        int? AlternativeIndex, string State, Guid RaisedBy, string? RaisedAgentName, DateTimeOffset RaisedAt, Guid? AnsweredBy,
        string? AnsweredAgentName, DateTimeOffset? AnsweredAt, bool FromDescription);

    private sealed record EventRow(
        Guid? DecisionId, string Kind, string? FromAnswer, string? ToAnswer, string? ToKind, string? Note, Guid? ByUser, string? AgentName,
        DateTimeOffset At, bool FromDescription);

    private sealed record ChildRow(Guid Id, string Key, string Type, string Title, bool OwnerIsCreator, Guid? Owner, int OpenQuestions, int OpenDecisions);
}
