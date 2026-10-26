using Dapper;
using Npgsql;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index.Texts;

// An attachment's searchable text (docs/search.md): attachment.search_text, under its item. Attachments without search_text,
// archived ones and those of removed items (state category) produce nothing, so the writer drops their rows.
public sealed class AttachmentTextSource(NpgsqlDataSource dataSource) : ISearchTextSource
{
    public SearchSourceKind Kind => SearchSourceKind.Attachment;

    public async Task<IReadOnlyList<SearchSourceText>> TextsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<AttachmentTextRow>(new CommandDefinition(
            """
            SELECT a.id, w.project_id AS projectId, w.id AS workItemId, w.title AS itemTitle, a.search_text AS searchText
            FROM attachment a
            JOIN work_item w ON w.id = a.work_item_id
            JOIN state s ON s.id = w.state_id
            WHERE a.id = ANY(@ids) AND a.archived_at IS NULL AND a.search_text IS NOT NULL AND s.category <> 'removed'
            """,
            new { ids = ids.ToArray() }, cancellationToken: cancellationToken));
        return [.. rows.Where(r => !string.IsNullOrWhiteSpace(r.SearchText))
            .Select(r => new SearchSourceText(r.Id, r.ProjectId, r.WorkItemId, r.ItemTitle, [new SearchFieldText(SearchField.Attachment, r.SearchText.Trim())]))];
    }

    public async Task<IReadOnlyList<Guid>> AllIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<Guid>(new CommandDefinition(
            """
            SELECT a.id FROM attachment a
            JOIN work_item w ON w.id = a.work_item_id
            JOIN state s ON s.id = w.state_id
            WHERE a.archived_at IS NULL AND a.search_text IS NOT NULL AND btrim(a.search_text) <> '' AND s.category <> 'removed'
            """,
            cancellationToken: cancellationToken))];
    }

    private sealed record AttachmentTextRow(Guid Id, Guid ProjectId, Guid WorkItemId, string ItemTitle, string SearchText);
}
