using System.Data.Common;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.Application.Features.IssueIntake.Records;

// IMirroredComments over the comment store and github_issue_comment. Scoped, because ICommentStore is.
public sealed class DbMirroredComments(NpgsqlDataSource dataSource, ICommentStore comments) : IMirroredComments
{
    private const string AgentNamePrefix = "GitHub @";

    public Task<Guid?> FindByUrlAsync(string htmlUrl, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => dataSource.RunAsync(
            (connection, tx) => connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
                "SELECT comment_id FROM github_issue_comment WHERE html_url = @htmlUrl", new { htmlUrl }, tx, cancellationToken: cancellationToken)),
            transaction,
            cancellationToken);

    public async Task<Guid> AddAsync(MirroredComment comment, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var connection = transaction.Connection!;
        var workItemId = await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(
            "SELECT work_item_id FROM github_issue WHERE id = @GitHubIssueId", new { comment.GitHubIssueId }, transaction, cancellationToken: cancellationToken))
            ?? throw new InvalidOperationException($"github_issue {comment.GitHubIssueId} does not exist.");
        var added = await comments.AddAsAgentAsync(
            workItemId, new CommentCreate(comment.Body), AgentNamePrefix + comment.AuthorLogin, comment.RunAsUserId, transaction, cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO github_issue_comment (comment_id, github_issue_id, github_comment_id, html_url, author_login, created_at)
            VALUES (@commentId, @GitHubIssueId, @GitHubCommentId, @HtmlUrl, @AuthorLogin, @CreatedAt)
            """,
            new { commentId = added.Id, comment.GitHubIssueId, comment.GitHubCommentId, comment.HtmlUrl, comment.AuthorLogin, CreatedAt = comment.CreatedAt.ToUniversalTime() },
            transaction,
            cancellationToken: cancellationToken));
        return added.Id;
    }
}
