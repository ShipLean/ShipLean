using System.Text.RegularExpressions;
using Dapper;
using Npgsql;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.TaskChanges;

namespace ShipLean.Application.Features.TaskChanges.Comments;

// ICodeComments over code_comment. A comment is anchored to one of the item's live commits; a reply carries only its parent.
// Writes stamp the caller (ICurrentUser, and the agent from AgentStamp) and publish codeCommentsChanged after commit.
public sealed partial class CodeComments(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    ICurrentAgent currentAgent,
    MarkdownRenderer markdown,
    ICodeHost host,
    TimeProvider clock,
    IProjectEvents events) : ICodeComments
{
    private const string Columns =
        """
        c.id, c.parent_id AS ParentId, r.path AS Repo, c.sha, c.path, c.side, c.line, c.start_line AS StartLine, c.line_text AS LineText,
        c.body, c.body_html AS BodyHtml, COALESCE(c.author_agent, u.name, '') AS "By", c.author_agent IS NOT NULL AS ByAgent,
        c.created_at AS CreatedAt, c.resolved_at AS ResolvedAt, c.outdated_at AS OutdatedAt, c.sent_back_at AS SentBackAt
        """;

    private const string From =
        """
        FROM code_comment c
        LEFT JOIN code_repository r ON r.id = c.repository_id
        LEFT JOIN app_user u ON u.id = c.author_id
        """;

    public async Task<CodeComment> AddAsync(Guid workItemId, NewCodeComment comment, CancellationToken cancellationToken = default)
    {
        var body = Body(comment.Body, "body");
        if (comment.Line < 1 || (comment.StartLine is { } start && (start < 1 || start > comment.Line)))
        {
            throw ApiException.Validation("Comment on a line that is in the diff.", "line");
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var anchor = await LiveCommitAsync(connection, workItemId, comment, cancellationToken);
        var id = Guid.NewGuid();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO code_comment (id, work_item_id, repository_id, sha, path, side, line, start_line, line_text, body, body_html, author_id, author_agent, created_at)
            VALUES (@id, @workItemId, @repositoryId, @sha, @path, @side, @line, @startLine, @lineText, @body, @bodyHtml, @userId, @agent, @now)
            """,
            new
            {
                id, workItemId, anchor.RepositoryId, anchor.Sha, comment.Path, side = comment.Side == CodeSide.Old ? "old" : "new",
                comment.Line, comment.StartLine, lineText = comment.LineText, body = body.Markdown, bodyHtml = body.Html,
                userId = currentUser.Id, agent = AgentStamp.Of(currentAgent), now = clock.GetUtcNow(),
            }, cancellationToken: cancellationToken));
        await PublishAsync(connection, workItemId, cancellationToken);
        return await ReadAsync(connection, workItemId, id, cancellationToken);
    }

    public async Task<CodeComment> ReplyAsync(Guid workItemId, Guid commentId, string body, CancellationToken cancellationToken = default)
    {
        var rendered = Body(body, "body");
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireTopLevelAsync(connection, null, workItemId, commentId, "id", cancellationToken);
        var id = Guid.NewGuid();
        await InsertReplyAsync(connection, null, workItemId, commentId, id, rendered, cancellationToken);
        await PublishAsync(connection, workItemId, cancellationToken);
        return await ReadAsync(connection, workItemId, id, cancellationToken);
    }

    public async Task ResolveAsync(Guid workItemId, Guid commentId, bool resolved, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireTopLevelAsync(connection, null, workItemId, commentId, "id", cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            "UPDATE code_comment SET resolved_at = @at, resolved_by = @by WHERE id = @commentId",
            new { commentId, at = resolved ? clock.GetUtcNow() : (DateTimeOffset?)null, by = resolved ? currentUser.Id : (Guid?)null }, cancellationToken: cancellationToken));
        await PublishAsync(connection, workItemId, cancellationToken);
    }

    public async Task<IReadOnlyList<CodeComment>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ListAsync(connection, workItemId, false, cancellationToken);
    }

    public async Task<IReadOnlyList<CodeComment>> OpenAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return await ListAsync(connection, workItemId, true, cancellationToken);
    }

    public async Task AnswerAsync(Guid workItemId, IReadOnlyList<CodeCommentAnswer> answers, CancellationToken cancellationToken = default)
    {
        var rendered = answers.Select(a => (a.Id, Body: Body(a.Reply, "answered_comments"))).ToList();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
        foreach (var (id, _) in rendered)
        {
            await RequireTopLevelAsync(connection, transaction, workItemId, id, "answered_comments", cancellationToken);
        }

        var agent = AgentStamp.Of(currentAgent);
        foreach (var (id, reply) in rendered)
        {
            var last = await connection.QuerySingleOrDefaultAsync<LastReply>(new CommandDefinition(
                """
                SELECT c.body, c.author_id AS AuthorId, c.author_agent AS AuthorAgent
                FROM code_comment c WHERE c.parent_id = @id AND c.archived_at IS NULL ORDER BY c.created_at DESC, c.id DESC LIMIT 1
                """, new { id }, transaction, cancellationToken: cancellationToken));
            var resolved = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
                "SELECT resolved_at IS NOT NULL FROM code_comment WHERE id = @id", new { id }, transaction, cancellationToken: cancellationToken));
            var already = resolved && last is not null && last.Body == reply.Markdown && last.AuthorId == currentUser.Id && last.AuthorAgent == agent;
            if (already)
            {
                continue;
            }

            await InsertReplyAsync(connection, transaction, workItemId, id, Guid.NewGuid(), reply, cancellationToken);
            await connection.ExecuteAsync(new CommandDefinition(
                "UPDATE code_comment SET resolved_at = COALESCE(resolved_at, @now), resolved_by = COALESCE(resolved_by, @by) WHERE id = @id",
                new { id, now = clock.GetUtcNow(), by = currentUser.Id }, transaction, cancellationToken: cancellationToken));
        }

        await transaction.CommitAsync(cancellationToken);
        await PublishAsync(connection, workItemId, cancellationToken);
    }

    public async Task MarkOutdatedAsync(Guid workItemId, IReadOnlyList<TaskCommit> added, CancellationToken cancellationToken = default)
    {
        if (added.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var open = (await connection.QueryAsync<OpenAnchor>(new CommandDefinition(
            """
            SELECT c.id, r.path AS Repo, c.sha, c.path, c.side, c.line, c.start_line AS StartLine
            FROM code_comment c JOIN code_repository r ON r.id = c.repository_id
            WHERE c.work_item_id = @workItemId AND c.parent_id IS NULL AND c.archived_at IS NULL AND c.resolved_at IS NULL AND c.outdated_at IS NULL
            """, new { workItemId }, cancellationToken: cancellationToken))).ToList();
        if (open.Count == 0)
        {
            return;
        }

        var patches = new Dictionary<Guid, string?>();
        var changed = false;
        foreach (var comment in open)
        {
            foreach (var commit in added.Where(c => c.Repo.Name == comment.Repo && c.Sha != comment.Sha))
            {
                if (!patches.TryGetValue(commit.Id, out var patch))
                {
                    var read = await host.ReadPatchAsync(commit.Repo, commit.Sha, cancellationToken);
                    patch = patches[commit.Id] = read.Status == CommitReadStatus.Found ? read.UnifiedDiff : null;
                }

                var side = comment.Side == "old" ? CodeSide.Old : CodeSide.New;
                if (patch is not null && HunkOverlap.Touches(patch, comment.Path, side, comment.Line, comment.StartLine))
                {
                    await connection.ExecuteAsync(new CommandDefinition(
                        "UPDATE code_comment SET outdated_at = @now WHERE id = @id", new { id = comment.Id, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
                    changed = true;
                    break;
                }
            }
        }

        if (changed)
        {
            await PublishAsync(connection, workItemId, cancellationToken);
        }
    }

    private (string Markdown, string Html) Body(string? body, string field)
    {
        var text = body?.Trim();
        if (string.IsNullOrEmpty(text))
        {
            throw ApiException.Validation("Write something first.", field);
        }

        return (text, markdown.Render(text).Html);
    }

    // The item's live commit that the anchor names: the repository by its path, the sha in full or as a prefix of 7 or more.
    private static async Task<Anchor> LiveCommitAsync(NpgsqlConnection connection, Guid workItemId, NewCodeComment comment, CancellationToken cancellationToken)
    {
        var sha = comment.Sha?.Trim().ToLowerInvariant() ?? string.Empty;
        var found = ShaPattern().IsMatch(sha)
            ? await connection.QueryFirstOrDefaultAsync<Anchor>(new CommandDefinition(
                """
                SELECT r.id AS RepositoryId, w.sha
                FROM work_item_commit w JOIN code_repository r ON r.id = w.repository_id
                WHERE w.work_item_id = @workItemId AND w.archived_at IS NULL AND r.path = @repo AND w.sha LIKE @prefix
                ORDER BY w.position LIMIT 1
                """, new { workItemId, repo = comment.Repo?.Trim().ToLowerInvariant(), prefix = sha + "%" }, cancellationToken: cancellationToken))
            : null;
        return found ?? throw ApiException.Validation("That commit is not one of this item's commits.", "sha");
    }

    private static async Task RequireTopLevelAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid workItemId, Guid commentId, string field, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleOrDefaultAsync<ParentOf>(new CommandDefinition(
            "SELECT parent_id AS ParentId FROM code_comment WHERE id = @commentId AND work_item_id = @workItemId AND archived_at IS NULL",
            new { commentId, workItemId }, transaction, cancellationToken: cancellationToken));
        if (row is null)
        {
            throw ApiException.NotFound("That comment does not exist.", field);
        }

        if (row.ParentId is not null)
        {
            throw ApiException.Validation("That is a reply; comment on the line it belongs to.", field);
        }
    }

    private async Task InsertReplyAsync(
        NpgsqlConnection connection, NpgsqlTransaction? transaction, Guid workItemId, Guid parentId, Guid id, (string Markdown, string Html) body, CancellationToken cancellationToken)
        => await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO code_comment (id, work_item_id, parent_id, body, body_html, author_id, author_agent, created_at)
            VALUES (@id, @workItemId, @parentId, @body, @bodyHtml, @userId, @agent, @now)
            """,
            new { id, workItemId, parentId, body = body.Markdown, bodyHtml = body.Html, userId = currentUser.Id, agent = AgentStamp.Of(currentAgent), now = clock.GetUtcNow() },
            transaction, cancellationToken: cancellationToken));

    private static async Task<CodeComment> ReadAsync(NpgsqlConnection connection, Guid workItemId, Guid id, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} {From} WHERE c.id = @id AND c.work_item_id = @workItemId", new { id, workItemId }, cancellationToken: cancellationToken));
        return row.ToComment([]);
    }

    private static async Task<IReadOnlyList<CodeComment>> ListAsync(NpgsqlConnection connection, Guid workItemId, bool openOnly, CancellationToken cancellationToken)
    {
        var rows = (await connection.QueryAsync<Row>(new CommandDefinition(
            $"SELECT {Columns} {From} WHERE c.work_item_id = @workItemId AND c.archived_at IS NULL ORDER BY c.created_at, c.id",
            new { workItemId }, cancellationToken: cancellationToken))).ToList();
        var replies = rows.Where(r => r.ParentId is not null).ToLookup(r => r.ParentId!.Value);
        return [.. rows
            .Where(r => r.ParentId is null && (!openOnly || r.ResolvedAt is null))
            .Select(r => r.ToComment([.. replies[r.Id].Select(x => x.ToComment([]))]))];
    }

    private async Task PublishAsync(NpgsqlConnection connection, Guid workItemId, CancellationToken cancellationToken)
    {
        var projectId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "SELECT project_id FROM work_item WHERE id = @workItemId", new { workItemId }, cancellationToken: cancellationToken));
        await events.PublishAsync(new CodeCommentsChanged(projectId, workItemId));
    }

    [GeneratedRegex("^[0-9a-f]{7,64}$")]
    private static partial Regex ShaPattern();

    private sealed record ParentOf(Guid? ParentId);

    private sealed record Anchor(Guid RepositoryId, string Sha);

    private sealed record LastReply(string Body, Guid? AuthorId, string? AuthorAgent);

    private sealed record OpenAnchor(Guid Id, string Repo, string Sha, string Path, string Side, int Line, int? StartLine);

    private sealed record Row(
        Guid Id, Guid? ParentId, string? Repo, string? Sha, string? Path, string? Side, int? Line, int? StartLine, string? LineText,
        string Body, string BodyHtml, string By, bool ByAgent, DateTimeOffset CreatedAt, DateTimeOffset? ResolvedAt, DateTimeOffset? OutdatedAt, DateTimeOffset? SentBackAt)
    {
        public CodeComment ToComment(IReadOnlyList<CodeComment> replies) => new(
            Id, ParentId, Repo, Sha, Path, Side is null ? null : Side == "old" ? CodeSide.Old : CodeSide.New, Line, StartLine, LineText,
            Body, BodyHtml, By, ByAgent, CreatedAt, ResolvedAt, OutdatedAt, SentBackAt, replies);
    }
}
