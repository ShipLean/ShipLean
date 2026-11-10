using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.Inbox;

// IMentionWrites over comment_mention. A mention is found only when it is the current user's, and the user must be a Member of
// its item's project; anything else is NotFound, the same as an unknown id. Each write locks the mention row in one
// transaction; a reply publishes CommentsChanged after commit.
public sealed class MentionWrites(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    ICurrentAgent currentAgent,
    IProjectAccess access,
    ICommentStore comments,
    TimeProvider clock,
    IProjectEvents events) : IMentionWrites
{
    private const string NotFoundMessage = "Mention not found.";

    public async Task SeenAsync(Guid mentionId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var mention = await MineAsync(connection, transaction, mentionId, cancellationToken);
        if (mention.State == "open")
        {
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE comment_mention SET state = 'seen', closed_at = @now WHERE id = @mentionId",
                new { mentionId, now = clock.GetUtcNow() }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<Guid> ReplyAsync(Guid mentionId, string body, CancellationToken cancellationToken = default)
    {
        // Before any read: the 400 depends only on the body, so it tells nothing about whose mention this is.
        if (string.IsNullOrWhiteSpace(body))
        {
            throw ApiException.Validation("Write a reply.", "body");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        var mention = await MineAsync(connection, transaction, mentionId, cancellationToken);
        if (mention.State == "withdrawn")
        {
            throw ApiException.Conflict("This mention was withdrawn: its comment no longer names you.");
        }

        var prefix = await PrefixAsync(connection, transaction, mention, cancellationToken);
        var comment = await comments.AddInTransactionAsync(
            mention.WorkItemId, new CommentCreate(prefix + body.Trim()), AgentStamp.Of(currentAgent), currentUser.Id, transaction, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE comment_mention SET state = 'replied', closed_at = @now, reply_comment_id = @commentId WHERE id = @mentionId",
            new { mentionId, now = clock.GetUtcNow(), commentId = comment.Id }, transaction, cancellationToken: cancellationToken));
        await transaction.CommitAsync(cancellationToken);
        await events.PublishAsync(new CommentsChanged(mention.ProjectId, mention.WorkItemId));
        return comment.Id;
    }

    // The current user's mention, locked for this transaction, once the user is checked as a Member of its item's project.
    private async Task<Mention> MineAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Guid mentionId, CancellationToken cancellationToken)
    {
        var mention = await connection.QuerySingleOrDefaultAsync<Mention>(new CommandDefinition(
            """
            SELECT m.work_item_id AS workItemId, wi.project_id AS projectId, m.state, c.author_id AS authorId
            FROM comment_mention m
            JOIN work_item wi ON wi.id = m.work_item_id
            JOIN comment c ON c.id = m.comment_id
            WHERE m.id = @mentionId AND m.user_id = @userId
            FOR UPDATE OF m
            """,
            new { mentionId, userId = currentUser.Id }, transaction, cancellationToken: cancellationToken))
            ?? throw ApiException.NotFound(NotFoundMessage);
        await access.RequireAsync(ProjectItem.WorkItem, mention.WorkItemId, ProjectRole.Member, cancellationToken);
        return mention;
    }

    // "@<author's full name>, " for the person whose comment named the user; none when that is the user or they have left the project.
    private async Task<string> PrefixAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, Mention mention, CancellationToken cancellationToken)
    {
        if (mention.AuthorId is not { } author || author == currentUser.Id)
        {
            return string.Empty;
        }

        var name = await connection.ExecuteScalarAsync<string?>(new CommandDefinition(
            """
            SELECT u.name FROM app_user u
            JOIN project_member member ON member.user_id = u.id AND member.project_id = @projectId AND member.archived_at IS NULL
            WHERE u.id = @author
            """,
            new { author, projectId = mention.ProjectId }, transaction, cancellationToken: cancellationToken));
        return name is null ? string.Empty : $"@{name}, ";
    }

    private sealed record Mention(Guid WorkItemId, Guid ProjectId, string State, Guid? AuthorId);
}
