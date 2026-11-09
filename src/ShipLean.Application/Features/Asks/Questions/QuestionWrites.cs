using Dapper;
using Npgsql;
using ShipLean.Application.Features.Asks.Sql;
using ShipLean.Contracts;
using ShipLean.Contracts.Asks;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.Asks.Questions;

// IQuestionWrites over the question, ask_event, comment_ask_tag and comment tables. Every write is one transaction under AskNumbers'
// per-item lock, and publishes after commit. The actor is ICurrentUser, stamped with AgentStamp (null for a person on the web).
public sealed class QuestionWrites(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    ICurrentAgent currentAgent,
    ICommentStore comments,
    TimeProvider clock,
    IProjectEvents events) : IQuestionWrites
{
    private const int MaxText = 2000;

    public async Task<AskedQuestion> AskAsync(Guid workItemId, string text, CancellationToken cancellationToken = default)
    {
        var cleaned = text?.Trim();
        if (string.IsNullOrEmpty(cleaned))
        {
            throw ApiException.Validation("Say what you are asking.", "text");
        }

        if (cleaned.Length > MaxText)
        {
            throw ApiException.Validation($"A question can be at most {MaxText} characters.", "text");
        }

        var agentName = AgentStamp.Of(currentAgent);
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AskNumbers.LockAsync(transaction, workItemId, cancellationToken);
        var projectId = await ProjectOfAsync(connection, transaction, workItemId, cancellationToken);
        var number = await AskNumbers.NextQuestionAsync(transaction, workItemId, cancellationToken);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO question (id, work_item_id, number, text, state, created_by_user_id, created_agent_name, created_at)
            VALUES (@id, @workItemId, @number, @text, 'open', @userId, @agentName, @now)
            """,
            new { id, workItemId, number, text = cleaned, userId = currentUser.Id, agentName, now }, transaction, cancellationToken: cancellationToken));
        await AskEventWriter.InsertAsync(transaction, new AskEventRow(
            Guid.NewGuid(), workItemId, null, id, "asked", null, null, null, null, currentUser.Id, agentName, now, false), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new AsksChanged(projectId, workItemId));
        return new AskedQuestion(number, $"Q{number}");
    }

    public async Task<Guid> AnswerAsync(Guid workItemId, int number, string body, CancellationToken cancellationToken = default)
    {
        var agentName = AgentStamp.Of(currentAgent);
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AskNumbers.LockAsync(transaction, workItemId, cancellationToken);
        var projectId = await ProjectOfAsync(connection, transaction, workItemId, cancellationToken);
        var question = await OpenQuestionAsync(connection, transaction, workItemId, number, cancellationToken);

        // The comment goes first: question.answer_comment_id is a foreign key to it. A body the store refuses rolls everything back.
        var comment = await comments.AddInTransactionAsync(workItemId, new CommentCreate(body), agentName, currentUser.Id, transaction, cancellationToken);
        await AskTagWriter.InsertAsync(transaction, comment.Id, "answer", null, question, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            UPDATE question SET state = 'answered', answer_comment_id = @commentId, answered_by_user_id = @userId, answered_at = @now
            WHERE id = @question
            """,
            new { commentId = comment.Id, userId = currentUser.Id, now, question }, transaction, cancellationToken: cancellationToken));
        await AskEventWriter.InsertAsync(transaction, new AskEventRow(
            Guid.NewGuid(), workItemId, null, question, "answered", null, null, null, null, currentUser.Id, agentName, now, false), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new AsksChanged(projectId, workItemId));
        await events.PublishAsync(new CommentsChanged(projectId, workItemId));
        return comment.Id;
    }

    public async Task WithdrawAsync(Guid workItemId, int number, CancellationToken cancellationToken = default)
    {
        var agentName = AgentStamp.Of(currentAgent);
        var now = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        await AskNumbers.LockAsync(transaction, workItemId, cancellationToken);
        var projectId = await ProjectOfAsync(connection, transaction, workItemId, cancellationToken);
        var question = await OpenQuestionAsync(connection, transaction, workItemId, number, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE question SET state = 'withdrawn', withdrawn_by_user_id = @userId, withdrawn_at = @now WHERE id = @question",
            new { userId = currentUser.Id, now, question }, transaction, cancellationToken: cancellationToken));
        await AskEventWriter.InsertAsync(transaction, new AskEventRow(
            Guid.NewGuid(), workItemId, null, question, "withdrawn", null, null, null, null, currentUser.Id, agentName, now, false), cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new AsksChanged(projectId, workItemId));
    }

    private static async Task<Guid> ProjectOfAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, CancellationToken cancellationToken)
        => await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "SELECT project_id FROM work_item WHERE id = @workItemId", new { workItemId }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("That work item does not exist.");

    // The question's id; NotFound for an unknown number, Conflict when it is no longer open.
    private static async Task<Guid> OpenQuestionAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid workItemId, int number, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<(Guid Id, string State)?>(new CommandDefinition(
            "SELECT id, state FROM question WHERE work_item_id = @workItemId AND number = @number",
            new { workItemId, number }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound($"Q{number} does not exist on this work item.");
        return row.State == "open" ? row.Id : throw ApiException.Conflict($"Q{number} is {row.State}: only an open question can change.");
    }
}
