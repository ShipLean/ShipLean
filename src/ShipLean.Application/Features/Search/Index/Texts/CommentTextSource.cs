using Dapper;
using Npgsql;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index.Texts;

// A comment's searchable text (docs/search.md): its body, under its item. Archived comments and comments of removed items
// (state category) produce nothing, so the writer drops their rows. The writer adds the item title as the chunk prefix.
public sealed class CommentTextSource(NpgsqlDataSource dataSource) : ISearchTextSource
{
    public SearchSourceKind Kind => SearchSourceKind.Comment;

    public async Task<IReadOnlyList<SearchSourceText>> TextsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<CommentTextRow>(new CommandDefinition(
            """
            SELECT c.id, w.project_id AS projectId, w.id AS workItemId, w.title AS itemTitle, c.body
            FROM comment c
            JOIN work_item w ON w.id = c.work_item_id
            JOIN state s ON s.id = w.state_id
            WHERE c.id = ANY(@ids) AND c.archived_at IS NULL AND s.category <> 'removed'
            """,
            new { ids = ids.ToArray() }, cancellationToken: cancellationToken));
        return [.. rows.Where(r => !string.IsNullOrWhiteSpace(r.Body))
            .Select(r => new SearchSourceText(r.Id, r.ProjectId, r.WorkItemId, r.ItemTitle, [new SearchFieldText(SearchField.Comment, r.Body.Trim())]))];
    }

    public async Task<IReadOnlyList<Guid>> AllIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<Guid>(new CommandDefinition(
            """
            SELECT c.id FROM comment c
            JOIN work_item w ON w.id = c.work_item_id
            JOIN state s ON s.id = w.state_id
            WHERE c.archived_at IS NULL AND s.category <> 'removed' AND btrim(c.body) <> ''
            """,
            cancellationToken: cancellationToken))];
    }

    private sealed record CommentTextRow(Guid Id, Guid ProjectId, Guid WorkItemId, string ItemTitle, string Body);
}
