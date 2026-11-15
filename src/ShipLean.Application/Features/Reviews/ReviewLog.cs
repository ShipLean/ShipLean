using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Reviews;
using ShipLean.Contracts.Views.Timeline;

namespace ShipLean.Application.Features.Reviews;

// IReviewLog over review_event. Appends one row in a transaction under a per-task advisory lock, so two reviews logged together
// get rounds 1 and 2. The row is never edited or removed. Verdicts are stored as approved | changesRequested.
public sealed class ReviewLog(NpgsqlDataSource dataSource, ICurrentUser currentUser, ICurrentAgent currentAgent, TimeProvider clock) : IReviewLog
{
    private const int MaxNote = 2000;

    public async Task<LoggedReview> AppendAsync(ReviewInput input, CancellationToken cancellationToken = default)
    {
        var note = string.IsNullOrWhiteSpace(input.Note) ? null : input.Note.Trim();
        if (note is { Length: > MaxNote })
        {
            throw ApiException.Validation($"A review note can be at most {MaxNote} characters.", "note");
        }

        var id = Guid.NewGuid();
        var at = clock.GetUtcNow();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var type = await connection.QuerySingleOrDefaultAsync<string>(new CommandDefinition(
            "SELECT type FROM work_item WHERE id = @workItemId", new { workItemId = input.WorkItemId }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound("No work item with that key was found.");
        if (!string.Equals(type, "task", StringComparison.OrdinalIgnoreCase))
        {
            throw ApiException.Validation("Only a task can be reviewed: log_review takes the key of a task.", "item");
        }

        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended(@key, 0))", new { key = $"review_event:{input.WorkItemId}" }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO review_event (id, work_item_id, verdict, note, by_user_id, agent_name, at)
            VALUES (@id, @workItemId, @verdict, @note, @userId, @agentName, @at)
            """,
            new
            {
                id,
                workItemId = input.WorkItemId,
                verdict = input.Verdict == ReviewVerdict.Approved ? "approved" : "changesRequested",
                note,
                userId = currentUser.Id,
                agentName = AgentStamp.Of(currentAgent),
                at,
            },
            transaction, cancellationToken: cancellationToken));
        var round = await connection.QuerySingleAsync<int>(new CommandDefinition(
            "SELECT count(*)::int FROM review_event WHERE work_item_id = @workItemId", new { workItemId = input.WorkItemId }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        return new LoggedReview(id, input.Verdict, at, round);
    }
}
