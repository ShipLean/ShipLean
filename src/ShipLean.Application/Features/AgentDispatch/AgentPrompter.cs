using Dapper;
using Microsoft.Extensions.Logging;
using Npgsql;
using ShipLean.Application.Features.AgentDispatch.Rendering;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.AgentDispatch;

// Queues the follow-ups to a live session: a person's comment (hooked from CommentStore after commit) and Stop. Each becomes a
// `prompted` agent_delivery plus a `prompt` agent_activity row, written in one transaction that holds the session row's lock
// (SELECT ... FOR UPDATE), so the delivery's seq is max + 1 with no gap or duplicate. Stop also sets stop_requested_at; the session
// stays live until the agent answers (C2 turns its `response` into canceled) or AgentDeliverer cancels it after 10 minutes.
// A comment with an agent stamp is never a prompt: that is how an agent's own comments stay out of its own inbox.
public sealed class AgentPrompter(NpgsqlDataSource dataSource, AgentSessionNotifier notifier, TimeProvider time, ILogger<AgentPrompter> logger) : IAgentPrompts
{
    private readonly AgentContextLoader _loader = new(dataSource);

    // Never throws into the comment write that is already committed.
    public async Task CommentAddedAsync(Guid commentId, CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            var comment = await connection.QuerySingleOrDefaultAsync<AgentPromptCommentRow>(new CommandDefinition(
                "SELECT work_item_id AS workItemId, author_id AS authorId, agent_name AS agentName, body, created_at AS createdAt FROM comment WHERE id = @commentId AND archived_at IS NULL",
                new { commentId }, cancellationToken: cancellationToken));
            if (comment is not { AgentName: null, AuthorId: { } authorId })
            {
                return;
            }

            var author = await UserAsync(connection, authorId, cancellationToken);
            await QueueAsync(comment.WorkItemId, author, comment.Body, commentId.ToString(), null, comment.CreatedAt, null, cancellationToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Could not queue a prompted agent delivery for comment {CommentId}", commentId);
        }
    }

    // fallbackBaseUrl is the origin of the request, for an instance that has not set its base URL.
    public async Task<AgentPromptOutcome> StopAsync(Guid workItemId, Guid userId, string? fallbackBaseUrl, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var author = await UserAsync(connection, userId, cancellationToken);
        return await QueueAsync(workItemId, author, string.Empty, null, AgentPromptInput.StopSignal, time.GetUtcNow(), fallbackBaseUrl, cancellationToken);
    }

    private static Task<AgentUserInput> UserAsync(NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken)
        => connection.QuerySingleAsync<AgentUserInput>(new CommandDefinition(
            "SELECT id::text AS id, name, email FROM app_user WHERE id = @userId", new { userId }, cancellationToken: cancellationToken));

    private async Task<AgentPromptOutcome> QueueAsync(
        Guid workItemId, AgentUserInput author, string body, string? commentId, string? signal, DateTimeOffset at, string? fallbackBaseUrl, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var session = await connection.QuerySingleOrDefaultAsync<AgentSessionRow>(new CommandDefinition(
            $"""
            SELECT id, connection_id AS connectionId, created_by AS createdBy, created_at AS createdAt, repo_url AS repoUrl, base_branch AS baseBranch
            FROM agent_session WHERE work_item_id = @workItemId AND state IN {AgentSessionStates.LiveSql}
            """,
            new { workItemId }, cancellationToken: cancellationToken));
        if (session is null)
        {
            return AgentPromptOutcome.NoLiveSession;
        }

        var input = await _loader.LoadAsync(
            workItemId, session.Id, session.CreatedAt, session.ConnectionId, session.CreatedBy, new AgentRepositoryInput(session.RepoUrl, session.BaseBranch), fallbackBaseUrl, null, cancellationToken);

        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var locked = await connection.QuerySingleOrDefaultAsync<LockedSession>(new CommandDefinition(
            $"SELECT state, last_activity_at AS lastActivityAt, stop_requested_at AS stopRequestedAt FROM agent_session WHERE id = @id AND state IN {AgentSessionStates.LiveSql} FOR UPDATE",
            new { id = session.Id }, transaction, cancellationToken: cancellationToken));
        if (locked is null)
        {
            return AgentPromptOutcome.NoLiveSession;
        }

        if (signal is not null)
        {
            if (locked.StopRequestedAt is not null)
            {
                return AgentPromptOutcome.AlreadyStopping;
            }

            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE agent_session SET stop_requested_at = @at WHERE id = @id", new { id = session.Id, at }, transaction, cancellationToken: cancellationToken));
        }

        var activityId = Guid.CreateVersion7();
        var rendered = AgentContextRenderer.RenderPrompted(
            input, new AgentPromptInput(activityId.ToString(), author, at, body, commentId, signal), AgentSessionStates.ToLinearStatus(locked.State), locked.LastActivityAt).ToJsonString();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO agent_delivery (id, session_id, seq, action, body, next_attempt_at, status, created_at)
            SELECT @id, @sessionId, coalesce(max(seq), 0) + 1, 'prompted', @body::jsonb, @now, 'pending', @now FROM agent_delivery WHERE session_id = @sessionId
            """,
            new { id = Guid.CreateVersion7(), sessionId = session.Id, body = rendered, now = time.GetUtcNow() }, transaction, cancellationToken: cancellationToken));
        await connection.ExecuteAsync(new CommandDefinition(
            "INSERT INTO agent_activity (id, session_id, type, body, signal, created_at) VALUES (@id, @sessionId, 'prompt', @body, @signal, @at)",
            new { id = activityId, sessionId = session.Id, body = signal is null ? body : null, signal, at }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        await notifier.ChangedAsync(session.Id);
        return AgentPromptOutcome.Queued;
    }

    private sealed record LockedSession(string State, DateTimeOffset LastActivityAt, DateTimeOffset? StopRequestedAt);
}
