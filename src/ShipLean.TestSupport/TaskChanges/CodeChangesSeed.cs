using System.Net;
using Dapper;
using Npgsql;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.TestSupport.TaskChanges;

// Writes repository, commit, work_item_commit and comment rows straight into the cloned database, for API tests.
// Each method inserts one row and returns its id (a commit returns the work_item_commit id). Commit meta comes from FakeCodeHost.
public sealed class CodeChangesSeed(NpgsqlConnection connection)
{
    private int _position;

    // The repository row for repo, made if it is not there yet.
    public async Task<Guid> RepositoryAsync(RepoRef repo)
        => await connection.ExecuteScalarAsync<Guid>(
            """
            INSERT INTO code_repository (id, provider, host, path) VALUES (@id, @provider, @host, @path)
            ON CONFLICT (provider, host, path) DO UPDATE SET host = excluded.host
            RETURNING id
            """,
            new { id = Guid.NewGuid(), provider = repo.Provider.ToString().ToLowerInvariant(), host = repo.Host, path = repo.Path });

    // The code_commit row (once) and a live work_item_commit on the item, the next in order unless position is given.
    public async Task<Guid> CommitAsync(Guid workItemId, RepoRef repo, string sha, CommitSource via = CommitSource.AddCommits, Guid? addedBy = null, string? addedByAgent = null, int? position = null)
    {
        var meta = FakeCodeHost.MetaOf(repo.Path, sha);
        var repositoryId = await RepositoryAsync(repo);
        await connection.ExecuteAsync(
            """
            INSERT INTO code_commit (repository_id, sha, parent_shas, subject, author_name, authored_at, additions, deletions, files_changed, paths, read_at)
            VALUES (@repositoryId, @sha, @parents, @subject, @author, @authoredAt, @additions, @deletions, @filesChanged, @paths, now())
            ON CONFLICT DO NOTHING
            """,
            new
            {
                repositoryId,
                sha = meta.Sha,
                parents = meta.ParentShas.ToArray(),
                subject = meta.Subject,
                author = meta.AuthorName,
                authoredAt = meta.AuthoredAt.ToUniversalTime(),
                additions = meta.Additions,
                deletions = meta.Deletions,
                filesChanged = meta.FilesChanged,
                paths = meta.Paths.ToArray(),
            });
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO work_item_commit (id, work_item_id, repository_id, sha, position, added_via, added_by, added_by_agent)
            VALUES (@id, @workItemId, @repositoryId, @sha, @position, @via, @addedBy::uuid, @addedByAgent::text)
            """,
            new { id, workItemId, repositoryId, sha = meta.Sha, position = position ?? ++_position, via = Via(via), addedBy, addedByAgent });
        return id;
    }

    // A top-level comment on a line of a commit's diff. The sha may be short; it is stored in full.
    public async Task<Guid> CommentAsync(Guid workItemId, RepoRef repo, string sha, string path, CodeSide side, int line, string body, Guid? authorId = null, string? authorAgent = null, string lineText = "", int? startLine = null)
    {
        var repositoryId = await RepositoryAsync(repo);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO code_comment (id, work_item_id, repository_id, sha, path, side, line, start_line, line_text, body, body_html, author_id, author_agent)
            VALUES (@id, @workItemId, @repositoryId, @sha, @path, @side, @line, @startLine, @lineText, @body, @bodyHtml, @authorId::uuid, @authorAgent::text)
            """,
            new
            {
                id,
                workItemId,
                repositoryId,
                sha = FakeCodeHost.MetaOf(repo.Path, sha).Sha,
                path,
                side = side == CodeSide.Old ? "old" : "new",
                line,
                startLine,
                lineText,
                body,
                bodyHtml = $"<p>{WebUtility.HtmlEncode(body)}</p>",
                authorId,
                authorAgent,
            });
        return id;
    }

    // A reply to a comment: it carries only its parent.
    public async Task<Guid> ReplyAsync(Guid workItemId, Guid parentId, string body, Guid? authorId = null, string? authorAgent = null)
    {
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(
            """
            INSERT INTO code_comment (id, work_item_id, parent_id, body, body_html, author_id, author_agent)
            VALUES (@id, @workItemId, @parentId, @body, @bodyHtml, @authorId::uuid, @authorAgent::text)
            """,
            new { id, workItemId, parentId, body, bodyHtml = $"<p>{WebUtility.HtmlEncode(body)}</p>", authorId, authorAgent });
        return id;
    }

    private static string Via(CommitSource source) => source switch
    {
        CommitSource.ReportTask => "report_task",
        CommitSource.AddCommits => "add_commits",
        CommitSource.Form => "form",
        _ => "migration",
    };
}
