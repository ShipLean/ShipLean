using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Records;

// IMirroredComments over the comment store and github_issue_comment. Scoped, because ICommentStore is. AddAsync serialises on a
// transaction-scoped advisory lock per html_url, so a concurrent second delivery waits for the first to commit, then finds it.
// It publishes nothing (ICommentStore's transactional overload doesn't either): the caller publishes the result's Changed after commit.
public sealed class DbMirroredComments(NpgsqlDataSource dataSource, ICommentStore comments) : IMirroredComments
{
    public Task<Guid?> FindByUrlAsync(string htmlUrl, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => dataSource.RunAsync(
            (connection, tx) => FindAsync(connection, htmlUrl, tx, cancellationToken),
            transaction,
            cancellationToken);

    public async Task<MirroredCommentAdd> AddAsync(MirroredComment comment, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection!;
        await connection.ExecuteAsync(new CommandDefinition(
            "SELECT pg_advisory_xact_lock(hashtextextended('github_issue_comment:' || @HtmlUrl, 0))", new { comment.HtmlUrl }, transaction, cancellationToken: cancellationToken));
        if (await FindAsync(connection, comment.HtmlUrl, transaction, cancellationToken) is { } existing)
        {
            return new MirroredCommentAdd(existing, null);
        }

        var target = await connection.QuerySingleOrDefaultAsync<MirrorTarget>(new CommandDefinition(
            "SELECT w.id AS workItemId, w.project_id AS projectId FROM github_issue g JOIN work_item w ON w.id = g.work_item_id WHERE g.id = @GitHubIssueId",
            new { comment.GitHubIssueId }, transaction, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"github_issue {comment.GitHubIssueId} does not exist.");
        var added = await comments.AddAsAgentAsync(
            target.WorkItemId, new CommentCreate(comment.Body), MirroredComment.AgentNamePrefix + comment.AuthorLogin, comment.RunAsUserId, transaction, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO github_issue_comment (comment_id, github_issue_id, github_comment_id, html_url, author_login, body, created_at)
            VALUES (@commentId, @GitHubIssueId, @GitHubCommentId, @HtmlUrl, @AuthorLogin, @Body, @CreatedAt)
            """,
            new { commentId = added.Id, comment.GitHubIssueId, comment.GitHubCommentId, comment.HtmlUrl, comment.AuthorLogin, comment.Body, CreatedAt = comment.CreatedAt.ToUniversalTime() },
            transaction,
            cancellationToken: cancellationToken));
        return new MirroredCommentAdd(added.Id, new CommentsChanged(target.ProjectId, target.WorkItemId));
    }

    private static Task<Guid?> FindAsync(DbConnection connection, string htmlUrl, DbTransaction? transaction, CancellationToken cancellationToken)
        => connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT comment_id FROM github_issue_comment WHERE html_url = @htmlUrl", new { htmlUrl }, transaction, cancellationToken: cancellationToken));

    private sealed record MirrorTarget(Guid WorkItemId, Guid ProjectId);
}
