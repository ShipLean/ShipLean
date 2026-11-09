using Dapper;
using Npgsql;
using ShipLean.Application.Features.Asks.Sql;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.Asks.Decisions.Answer;

// IDecisionAnswers over the decision, ask_event, comment and comment_ask_tag tables. Each call is one transaction under AskNumbers'
// per-item lock, so the state it reads is the state it writes. The actor is ICurrentUser stamped with AgentStamp (null for a
// person). Usage is the caller's to track: the MCP and web paths count differently.
public sealed class DecisionAnswers(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    ICurrentAgent currentAgent,
    ICommentStore comments,
    TimeProvider clock,
    IProjectEvents events) : IDecisionAnswers
{
    private const int MaxText = 500;
    private const int MaxDiscussion = 10_000;

    public async Task AnswerAsync(Guid workItemId, DecisionAnswer answer, CancellationToken cancellationToken = default)
    {
        var change = answer.Action == DecisionAnswerAction.Change ? CheckedChange(answer) : null;
        var note = string.IsNullOrWhiteSpace(answer.Note) ? null : answer.Note.Trim();
        var actor = new Actor(currentUser.Id, AgentStamp.Of(currentAgent), clock.GetUtcNow());

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AskNumbers.LockAsync(transaction, workItemId, cancellationToken);
        var projectId = await ProjectOfAsync(connection, transaction, workItemId, cancellationToken);
        var decision = await FindAsync(connection, transaction, workItemId, answer.Number, cancellationToken);

        var commented = false;
        switch (answer.Action)
        {
            case DecisionAnswerAction.Confirm:
                if (decision.State != "open")
                {
                    throw ApiException.Conflict($"D{decision.Number} is {decision.State}: only an open decision can be confirmed.");
                }

                await ConfirmAsync(connection, transaction, workItemId, decision, actor, cancellationToken);
                break;
            case DecisionAnswerAction.Change:
                commented = await ChangeAsync(connection, transaction, workItemId, decision, change!, note, actor, cancellationToken);
                break;
            case DecisionAnswerAction.Withdraw:
                await WithdrawAsync(connection, transaction, workItemId, decision, note, actor, cancellationToken);
                break;
            default:
                throw ApiException.Validation("That is not a decision action.", "action");
        }

        await transaction.CommitAsync(cancellationToken);
        await PublishAsync(projectId, workItemId, commented);
    }

    public async Task<IReadOnlyList<int>> ConfirmOpenAsync(Guid workItemId, IReadOnlyList<int> numbers, CancellationToken cancellationToken = default)
    {
        var actor = new Actor(currentUser.Id, AgentStamp.Of(currentAgent), clock.GetUtcNow());

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AskNumbers.LockAsync(transaction, workItemId, cancellationToken);
        var projectId = await ProjectOfAsync(connection, transaction, workItemId, cancellationToken);

        var confirmed = new List<int>();
        foreach (var number in numbers.Distinct())
        {
            var decision = await FindAsync(connection, transaction, workItemId, number, cancellationToken);
            if (decision.State == "open")
            {
                await ConfirmAsync(connection, transaction, workItemId, decision, actor, cancellationToken);
                confirmed.Add(number);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        if (confirmed.Count > 0)
        {
            await PublishAsync(projectId, workItemId, false);
        }

        return confirmed;
    }

    public async Task DiscussAsync(Guid workItemId, int number, string body, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw ApiException.Validation("Write something to post.", "body");
        }

        if (body.Length > MaxDiscussion)
        {
            throw ApiException.Validation($"A comment is at most {MaxDiscussion:N0} characters.", "body");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AskNumbers.LockAsync(transaction, workItemId, cancellationToken);
        var projectId = await ProjectOfAsync(connection, transaction, workItemId, cancellationToken);
        var decision = await FindAsync(connection, transaction, workItemId, number, cancellationToken);

        var comment = await comments.AddInTransactionAsync(
            workItemId, new CommentCreate(body), AgentStamp.Of(currentAgent), currentUser.Id, transaction, cancellationToken);
        await AskTagWriter.InsertAsync(transaction, comment.Id, "about", decision.Id, null, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        await PublishAsync(projectId, workItemId, true);
    }

    private static async Task ConfirmAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, Row decision, Actor actor, CancellationToken cancellationToken)
    {
        // An open decision already holds the recommended answer (ck_decision_open_answer), so confirming keeps it.
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE decision SET state = 'confirmed', answered_by_user_id = @userId, answered_agent_name = @agentName,
                                answered_at = @now, updated_at = @now
            WHERE id = @id
            """,
            new { id = decision.Id, userId = actor.UserId, agentName = actor.AgentName, now = actor.Now }, transaction, cancellationToken: cancellationToken));
        await AskEventWriter.InsertAsync(transaction, new AskEventRow(
            Guid.NewGuid(), workItemId, decision.Id, null, "confirmed", null, decision.CurrentAnswer, decision.AnswerKind, null,
            actor.UserId, actor.AgentName, actor.Now, false), cancellationToken);
    }

    // True when it posted the comment for a change to a settled decision.
    private async Task<bool> ChangeAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, Row decision, Change change, string? note, Actor actor,
        CancellationToken cancellationToken)
    {
        if (decision.State == "withdrawn")
        {
            throw ApiException.Conflict($"D{decision.Number} is withdrawn: it cannot be changed.");
        }

        var (text, kind, index, state) = change.Kind switch
        {
            AnswerKind.Recommended => (decision.Recommended, "recommended", (int?)null, "confirmed"),
            AnswerKind.Alternative when change.AlternativeIndex is { } i && i < decision.Alternatives.Length
                => (decision.Alternatives[i], "alternative", (int?)i, "changed"),
            AnswerKind.Alternative => throw ApiException.Validation("Choose one of the alternatives.", "alternativeIndex"),
            _ => (change.Text!, "own", (int?)null, "changed"),
        };

        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE decision SET current_answer = @text, answer_kind = @kind, alternative_index = @index, state = @state,
                                answered_by_user_id = @userId, answered_agent_name = @agentName, answered_at = @now, updated_at = @now
            WHERE id = @id
            """,
            new { id = decision.Id, text, kind, index, state, userId = actor.UserId, agentName = actor.AgentName, now = actor.Now },
            transaction, cancellationToken: cancellationToken));
        await AskEventWriter.InsertAsync(transaction, new AskEventRow(
            Guid.NewGuid(), workItemId, decision.Id, null, "changed", decision.CurrentAnswer, text, kind, note,
            actor.UserId, actor.AgentName, actor.Now, false), cancellationToken);

        if (decision.State == "open")
        {
            return false;
        }

        var name = await connection.ExecuteScalarAsync<string>(new CommandDefinition(
            "SELECT name FROM app_user WHERE id = @id", new { id = actor.UserId }, transaction, cancellationToken: cancellationToken));
        var body = $"D{decision.Number} changed from “{decision.CurrentAnswer}” to “{text}” by {name}."
            + (note is null ? "" : $"\n\n{note}");
        var comment = await comments.AddInTransactionAsync(
            workItemId, new CommentCreate(body), actor.AgentName, actor.UserId, transaction, cancellationToken);
        await AskTagWriter.InsertAsync(transaction, comment.Id, "changed", decision.Id, null, cancellationToken);
        return true;
    }

    private static async Task WithdrawAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, Row decision, string? note, Actor actor, CancellationToken cancellationToken)
    {
        if (decision.State == "withdrawn")
        {
            throw ApiException.Conflict($"D{decision.Number} is already withdrawn.");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE decision SET state = 'withdrawn', updated_at = @now WHERE id = @id",
            new { id = decision.Id, now = actor.Now }, transaction, cancellationToken: cancellationToken));
        await AskEventWriter.InsertAsync(transaction, new AskEventRow(
            Guid.NewGuid(), workItemId, decision.Id, null, "withdrawn", decision.CurrentAnswer, null, null, note,
            actor.UserId, actor.AgentName, actor.Now, false), cancellationToken);
    }

    private async Task PublishAsync(Guid projectId, Guid workItemId, bool commented)
    {
        await events.PublishAsync(new AsksChanged(projectId, workItemId));
        if (commented)
        {
            await events.PublishAsync(new CommentsChanged(projectId, workItemId));
        }
    }

    private static Change CheckedChange(DecisionAnswer answer)
    {
        switch (answer.Kind)
        {
            case AnswerKind.Recommended:
                return new Change(AnswerKind.Recommended, null, null);
            case AnswerKind.Alternative:
                return answer.AlternativeIndex is >= 0
                    ? new Change(AnswerKind.Alternative, answer.AlternativeIndex, null)
                    : throw ApiException.Validation("Choose one of the alternatives.", "alternativeIndex");
            case AnswerKind.Own:
                var text = answer.Text?.Trim();
                if (string.IsNullOrEmpty(text))
                {
                    throw ApiException.Validation("Write your answer.", "text");
                }

                return text.Length > MaxText
                    ? throw ApiException.Validation($"An answer is at most {MaxText} characters.", "text")
                    : new Change(AnswerKind.Own, null, text);
            default:
                throw ApiException.Validation("Choose how to answer.", "kind");
        }
    }

    private static async Task<Guid> ProjectOfAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, CancellationToken cancellationToken)
        => await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT project_id FROM work_item WHERE id = @workItemId", new { workItemId }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("That work item does not exist.");

    private static async Task<Row> FindAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, int number, CancellationToken cancellationToken)
    {
        var found = await connection.QuerySingleOrDefaultAsync<Stored>(new CommandDefinition(
            """
            SELECT id AS Id, number AS Number, recommended AS Recommended, alternatives::text AS Alternatives, current_answer AS CurrentAnswer,
                   answer_kind AS AnswerKind, state AS State
            FROM decision WHERE work_item_id = @workItemId AND number = @number
            """,
            new { workItemId, number }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound($"D{number} does not exist on that work item.");
        return new Row(found.Id, found.Number, found.Recommended, System.Text.Json.JsonSerializer.Deserialize<string[]>(found.Alternatives)!,
            found.CurrentAnswer, found.AnswerKind, found.State);
    }

    private sealed record Actor(Guid UserId, string? AgentName, DateTimeOffset Now);

    private sealed record Change(AnswerKind Kind, int? AlternativeIndex, string? Text);

    private sealed record Stored(Guid Id, int Number, string Recommended, string Alternatives, string CurrentAnswer, string AnswerKind, string State);

    private sealed record Row(Guid Id, int Number, string Recommended, string[] Alternatives, string CurrentAnswer, string AnswerKind, string State);
}
