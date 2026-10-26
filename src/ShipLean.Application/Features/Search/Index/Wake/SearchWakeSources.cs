using Dapper;
using Npgsql;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index.Wake;

// Which search sources an event can have changed. An item event maps to the item; created, removed, and updated with a title
// or state change also map to all of its comments and attachments (archived ones too, so the writer drops their rows): their
// chunks carry the item title as a prefix, and a restored item must bring them back. Every other event type maps to nothing.
public sealed class SearchWakeSources(NpgsqlDataSource dataSource)
{
    public static bool Wakes(ProjectEvent projectEvent)
        => projectEvent is WorkItemCreated or WorkItemUpdated or WorkItemRemoved or CommentsChanged or AttachmentsChanged or ProjectChanged or SprintChanged;

    public async Task<IReadOnlyList<SearchSource>> ForAsync(IReadOnlyList<ProjectEvent> events, CancellationToken cancellationToken = default)
    {
        var sources = new HashSet<SearchSource>();
        var items = new HashSet<Guid>();
        var expanding = new HashSet<Guid>();
        var commentsOf = new HashSet<Guid>();
        var attachmentsOf = new HashSet<Guid>();
        foreach (var projectEvent in events)
        {
            switch (projectEvent)
            {
                case WorkItemCreated created:
                    items.Add(created.Id);
                    expanding.Add(created.Id);
                    break;
                case WorkItemUpdated updated:
                    items.Add(updated.Id);
                    if (updated.Fields.Contains(FieldIds.Title) || updated.Fields.Contains(FieldIds.State))
                    {
                        expanding.Add(updated.Id);
                    }

                    break;
                case WorkItemRemoved removed:
                    items.Add(removed.Id);
                    expanding.Add(removed.Id);
                    break;
                case CommentsChanged comments:
                    commentsOf.Add(comments.WorkItemId);
                    break;
                case AttachmentsChanged attachments:
                    attachmentsOf.Add(attachments.WorkItemId);
                    break;
                case ProjectChanged:
                    sources.Add(new SearchSource(SearchSourceKind.Project, projectEvent.ProjectId));
                    break;
                case SprintChanged sprint:
                    sources.Add(new SearchSource(SearchSourceKind.Sprint, sprint.SprintId));
                    break;
            }
        }

        foreach (var id in items)
        {
            sources.Add(new SearchSource(SearchSourceKind.Item, id));
        }

        commentsOf.UnionWith(expanding);
        attachmentsOf.UnionWith(expanding);
        if (commentsOf.Count > 0 || attachmentsOf.Count > 0)
        {
            await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
            foreach (var id in await IdsAsync(connection, "comment", commentsOf, cancellationToken))
            {
                sources.Add(new SearchSource(SearchSourceKind.Comment, id));
            }

            foreach (var id in await IdsAsync(connection, "attachment", attachmentsOf, cancellationToken))
            {
                sources.Add(new SearchSource(SearchSourceKind.Attachment, id));
            }
        }

        return [.. sources];
    }

    // table is one of two literals above, never input.
    private static async Task<IEnumerable<Guid>> IdsAsync(NpgsqlConnection connection, string table, HashSet<Guid> workItemIds, CancellationToken cancellationToken)
    {
        if (workItemIds.Count == 0)
        {
            return [];
        }

        var sql = table == "comment"
            ? "SELECT id FROM comment WHERE work_item_id = ANY(@ids)"
            : "SELECT id FROM attachment WHERE work_item_id = ANY(@ids)";
        return await connection.QueryAsync<Guid>(new CommandDefinition(sql, new { ids = workItemIds.ToArray() }, cancellationToken: cancellationToken));
    }
}
