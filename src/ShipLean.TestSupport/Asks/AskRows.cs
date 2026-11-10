using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Database;
using ShipLean.Application.Features.Asks.Sql;

namespace ShipLean.TestSupport.Asks;

// Reads the asks tables back as raw rows, for tests that assert what a writer stored. Enums are the stored strings, times UTC.
// Lists come in number order (decisions, questions), time order (events) or comment time order (tags).
public static class AskRows
{
    public sealed record Decision(
        Guid Id, Guid WorkItemId, int Number, string Question, string Recommended, string[] Alternatives, string CurrentAnswer,
        string AnswerKind, int? AlternativeIndex, string State, Guid CreatedByUserId, string? CreatedAgentName, DateTimeOffset CreatedAt,
        Guid? AnsweredByUserId, string? AnsweredAgentName, DateTimeOffset? AnsweredAt, bool FromDescription, DateTimeOffset UpdatedAt);

    // As read: alternatives is the jsonb array's text, parsed into Decision.Alternatives.
    private sealed record DecisionRow(
        Guid Id, Guid WorkItemId, int Number, string Question, string Recommended, string Alternatives, string CurrentAnswer,
        string AnswerKind, int? AlternativeIndex, string State, Guid CreatedByUserId, string? CreatedAgentName, DateTimeOffset CreatedAt,
        Guid? AnsweredByUserId, string? AnsweredAgentName, DateTimeOffset? AnsweredAt, bool FromDescription, DateTimeOffset UpdatedAt)
    {
        public Decision ToDecision() => new(
            Id, WorkItemId, Number, Question, Recommended, JsonSerializer.Deserialize<string[]>(Alternatives)!, CurrentAnswer, AnswerKind,
            AlternativeIndex, State, CreatedByUserId, CreatedAgentName, CreatedAt, AnsweredByUserId, AnsweredAgentName, AnsweredAt,
            FromDescription, UpdatedAt);
    }

    public sealed record Question(
        Guid Id, Guid WorkItemId, int Number, string Text, string State, Guid CreatedByUserId, string? CreatedAgentName, DateTimeOffset CreatedAt,
        Guid? AnswerCommentId, Guid? AnsweredByUserId, DateTimeOffset? AnsweredAt, Guid? WithdrawnByUserId, DateTimeOffset? WithdrawnAt);

    public sealed record Tag(Guid CommentId, string Kind, Guid? DecisionId, Guid? QuestionId, DateTimeOffset CreatedAt);

    private const string DecisionColumns =
        """
        id AS Id, work_item_id AS WorkItemId, number AS Number, question AS Question, recommended AS Recommended,
        alternatives::text AS Alternatives, current_answer AS CurrentAnswer,
        answer_kind AS AnswerKind, alternative_index AS AlternativeIndex, state AS State, created_by_user_id AS CreatedByUserId,
        created_agent_name AS CreatedAgentName, created_at AS CreatedAt, answered_by_user_id AS AnsweredByUserId,
        answered_agent_name AS AnsweredAgentName, answered_at AS AnsweredAt, from_description AS FromDescription, updated_at AS UpdatedAt
        """;

    private const string QuestionColumns =
        """
        id AS Id, work_item_id AS WorkItemId, number AS Number, text AS Text, state AS State, created_by_user_id AS CreatedByUserId,
        created_agent_name AS CreatedAgentName, created_at AS CreatedAt, answer_comment_id AS AnswerCommentId,
        answered_by_user_id AS AnsweredByUserId, answered_at AS AnsweredAt, withdrawn_by_user_id AS WithdrawnByUserId,
        withdrawn_at AS WithdrawnAt
        """;

    private const string DecisionById = $"SELECT {DecisionColumns} FROM decision WHERE id = @id";
    private const string DecisionsByItem = $"SELECT {DecisionColumns} FROM decision WHERE work_item_id = @workItemId ORDER BY number";
    private const string QuestionById = $"SELECT {QuestionColumns} FROM question WHERE id = @id";
    private const string QuestionsByItem = $"SELECT {QuestionColumns} FROM question WHERE work_item_id = @workItemId ORDER BY number";

    public static async Task<Decision> DecisionAsync(NpgsqlConnection connection, Guid id)
    {
        DapperTypeHandlers.Register();
        return (await connection.QuerySingleAsync<DecisionRow>(DecisionById, new { id })).ToDecision();
    }

    public static async Task<IReadOnlyList<Decision>> DecisionsAsync(NpgsqlConnection connection, Guid workItemId)
    {
        DapperTypeHandlers.Register();
        return [.. (await connection.QueryAsync<DecisionRow>(DecisionsByItem, new { workItemId })).Select(r => r.ToDecision())];
    }

    public static async Task<Question> QuestionAsync(NpgsqlConnection connection, Guid id)
    {
        DapperTypeHandlers.Register();
        return await connection.QuerySingleAsync<Question>(QuestionById, new { id });
    }

    public static async Task<IReadOnlyList<Question>> QuestionsAsync(NpgsqlConnection connection, Guid workItemId)
    {
        DapperTypeHandlers.Register();
        return [.. await connection.QueryAsync<Question>(
            QuestionsByItem, new { workItemId })];
    }

    public static async Task<IReadOnlyList<AskEventRow>> EventsAsync(NpgsqlConnection connection, Guid workItemId)
    {
        DapperTypeHandlers.Register();
        return [.. await connection.QueryAsync<AskEventRow>(
            """
            SELECT id AS Id, work_item_id AS WorkItemId, decision_id AS DecisionId, question_id AS QuestionId, kind AS Kind,
                   from_answer AS FromAnswer, to_answer AS ToAnswer, to_kind AS ToKind, note AS Note, by_user_id AS ByUserId,
                   agent_name AS AgentName, at AS At, from_description AS FromDescription
            FROM ask_event WHERE work_item_id = @workItemId ORDER BY at, id
            """,
            new { workItemId })];
    }

    // The tag on one comment, or null when the comment has none.
    public static async Task<Tag?> TagAsync(NpgsqlConnection connection, Guid commentId)
    {
        DapperTypeHandlers.Register();
        return await connection.QuerySingleOrDefaultAsync<Tag>(
            """
            SELECT comment_id AS CommentId, kind AS Kind, decision_id AS DecisionId, question_id AS QuestionId, created_at AS CreatedAt
            FROM comment_ask_tag WHERE comment_id = @commentId
            """,
            new { commentId });
    }

    public static async Task<IReadOnlyList<Tag>> TagsAsync(NpgsqlConnection connection, Guid workItemId)
    {
        DapperTypeHandlers.Register();
        return [.. await connection.QueryAsync<Tag>(
            """
            SELECT t.comment_id AS CommentId, t.kind AS Kind, t.decision_id AS DecisionId, t.question_id AS QuestionId, t.created_at AS CreatedAt
            FROM comment_ask_tag t JOIN comment c ON c.id = t.comment_id
            WHERE c.work_item_id = @workItemId ORDER BY c.created_at, t.comment_id
            """,
            new { workItemId })];
    }
}
