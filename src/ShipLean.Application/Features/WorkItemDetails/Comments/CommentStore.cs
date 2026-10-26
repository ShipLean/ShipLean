using Dapper;
using Npgsql;
using ShipLean.Application.Markdown;
using ShipLean.Contracts;
using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.WorkItemDetails.Comments;

// ICommentStore over Postgres. The author is ICurrentUser; an agent acting for them is stamped on the row (AgentStamp).
// Archiving sets archived_at and keeps the row. An import never touches the archived mark or the created time and writes
// updated_at = created_at, so "edited" (updated_at after created_at) is never true for an imported comment.
// Add, edit, supersede and archive publish commentsChanged after the write; an import publishes nothing (it reports through importProgress).
public sealed class CommentStore(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    ICurrentAgent currentAgent,
    MarkdownRenderer renderer,
    TimeProvider clock,
    IProjectEvents events) : ICommentStore
{
    private static readonly string SelectLive = $"SELECT {CommentRow.Columns} {CommentRow.From} WHERE c.id = @id AND c.archived_at IS NULL";

    public async Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireWorkItemAsync(connection, workItemId, cancellationToken);
        var rows = await connection.QueryAsync<CommentRow>(new CommandDefinition(
            $"SELECT {CommentRow.Columns} {CommentRow.From} WHERE c.work_item_id = @workItemId AND c.archived_at IS NULL ORDER BY c.created_at, c.id",
            new { workItemId },
            cancellationToken: cancellationToken));
        return [.. rows.Select(r => r.ToDto(renderer, clock))];
    }

    public async Task<CommentDto> AddAsync(Guid workItemId, CommentCreate create, CancellationToken cancellationToken = default)
    {
        var body = RequireBody(create?.Body);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireWorkItemAsync(connection, workItemId, cancellationToken);
        var id = Guid.NewGuid();
        var now = clock.GetUtcNow();
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO comment (id, work_item_id, author_id, body, created_at, updated_at, agent_name)
            VALUES (@id, @workItemId, @authorId, @body, @now, @now, @agentName)
            """,
            new { id, workItemId, authorId = currentUser.Id, body, now, agentName = AgentStamp.Of(currentAgent) },
            cancellationToken: cancellationToken));
        var added = await ReadAsync(connection, id, cancellationToken);
        await PublishChangedAsync(connection, workItemId, cancellationToken);
        return added;
    }

    public async Task<CommentDto> EditAsync(Guid id, CommentEdit edit, CancellationToken cancellationToken = default)
    {
        var body = RequireBody(edit?.Body);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // The author check and the update are one statement, so a comment archived in between is simply not found.
        var updated = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "UPDATE comment SET body = @body, updated_at = @now WHERE id = @id AND archived_at IS NULL AND author_id = @me RETURNING id",
            new { id, body, now = clock.GetUtcNow(), me = currentUser.Id },
            cancellationToken: cancellationToken));
        if (updated is null)
        {
            await ThrowWhyNotEditedAsync(connection, id, cancellationToken);
        }

        var row = await connection.QuerySingleAsync<CommentRow>(new CommandDefinition(
            $"SELECT {CommentRow.Columns} {CommentRow.From} WHERE c.id = @id", new { id }, cancellationToken: cancellationToken));
        var edited = row.ToDto(renderer, clock);
        await PublishChangedAsync(connection, edited.WorkItemId, cancellationToken);
        return edited;
    }

    public async Task<CommentDto> SupersedeAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        // The first mark stays: a second Mark superseded changes nothing.
        var workItemId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            """
            UPDATE comment SET superseded_at = COALESCE(superseded_at, @now), superseded_by = COALESCE(superseded_by, @me)
            WHERE id = @id AND archived_at IS NULL RETURNING work_item_id
            """,
            new { id, now = clock.GetUtcNow(), me = currentUser.Id },
            cancellationToken: cancellationToken));
        if (workItemId is null)
        {
            throw ApiException.NotFound("That comment does not exist.");
        }

        var superseded = await ReadAsync(connection, id, cancellationToken);
        await PublishChangedAsync(connection, workItemId.Value, cancellationToken);
        return superseded;
    }

    public async Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var workItemId = await connection.ExecuteScalarAsync<Guid?>(new CommandDefinition(
            "UPDATE comment SET archived_at = @now WHERE id = @id AND archived_at IS NULL RETURNING work_item_id",
            new { id, now = clock.GetUtcNow() },
            cancellationToken: cancellationToken));
        if (workItemId is null)
        {
            throw ApiException.NotFound("That comment does not exist.");
        }

        await PublishChangedAsync(connection, workItemId.Value, cancellationToken);
    }

    public async Task<Guid> ImportAsync(Guid workItemId, ImportedComment comment, CancellationToken cancellationToken = default)
    {
        var body = RequireBody(comment?.Body);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await RequireWorkItemAsync(connection, workItemId, cancellationToken);
        if (comment!.AuthorId is { } author
            && !await connection.ExecuteScalarAsync<bool>(new CommandDefinition("SELECT EXISTS (SELECT 1 FROM app_user WHERE id = @author)", new { author }, cancellationToken: cancellationToken)))
        {
            throw ApiException.NotFound("That user does not exist.");
        }

        return await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            """
            INSERT INTO comment (id, work_item_id, author_id, body, original_system, original_system_id, created_at, updated_at)
            VALUES (@id, @workItemId, @authorId, @body, @system, @systemId, @createdAt, @createdAt)
            ON CONFLICT (work_item_id, original_system, original_system_id) WHERE original_system IS NOT NULL
            DO UPDATE SET body = excluded.body, updated_at = comment.created_at
            RETURNING id
            """,
            new { id = Guid.NewGuid(), workItemId, authorId = comment.AuthorId, body, system = comment.OriginalSystem, systemId = comment.OriginalSystemId, createdAt = comment.CreatedAt },
            cancellationToken: cancellationToken));
    }

    private static async Task ThrowWhyNotEditedAsync(NpgsqlConnection connection, Guid id, CancellationToken cancellationToken)
    {
        var live = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM comment WHERE id = @id AND archived_at IS NULL)", new { id }, cancellationToken: cancellationToken));
        throw live ? ApiException.Forbidden("Only the author can edit a comment.") : ApiException.NotFound("That comment does not exist.");
    }

    private async Task PublishChangedAsync(NpgsqlConnection connection, Guid workItemId, CancellationToken cancellationToken)
    {
        var projectId = await connection.ExecuteScalarAsync<Guid>(new CommandDefinition(
            "SELECT project_id FROM work_item WHERE id = @workItemId", new { workItemId }, cancellationToken: cancellationToken));
        await events.PublishAsync(new CommentsChanged(projectId, workItemId));
    }

    private string RequireBody(string? body)
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            throw ApiException.Validation("A comment cannot be empty.");
        }

        renderer.Render(body);
        return body;
    }

    private static async Task RequireWorkItemAsync(NpgsqlConnection connection, Guid workItemId, CancellationToken cancellationToken)
    {
        var exists = await connection.ExecuteScalarAsync<bool>(new CommandDefinition(
            "SELECT EXISTS (SELECT 1 FROM work_item WHERE id = @workItemId)", new { workItemId }, cancellationToken: cancellationToken));
        if (!exists)
        {
            throw ApiException.NotFound("That work item does not exist.");
        }
    }

    private async Task<CommentDto> ReadAsync(NpgsqlConnection connection, Guid id, CancellationToken cancellationToken)
    {
        var row = await connection.QuerySingleAsync<CommentRow>(new CommandDefinition(SelectLive, new { id }, cancellationToken: cancellationToken));
        return row.ToDto(renderer, clock);
    }
}
