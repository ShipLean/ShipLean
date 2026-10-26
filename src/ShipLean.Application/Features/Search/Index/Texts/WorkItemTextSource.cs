using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index.Texts;

// An item's searchable text (docs/search.md): the title, the tags joined with a space, and the body (description, then each
// acceptance criterion, then repro steps, blank lines between). Removed items (state category) produce nothing, so the writer drops their rows.
public sealed class WorkItemTextSource(NpgsqlDataSource dataSource) : ISearchTextSource
{
    public SearchSourceKind Kind => SearchSourceKind.Item;

    public async Task<IReadOnlyList<SearchSourceText>> TextsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ItemTextRow>(new CommandDefinition(
            """
            SELECT w.id, w.project_id AS projectId, w.title, w.description, w.acceptance_criteria::text AS acceptanceCriteria,
                   w.repro_steps AS reproSteps, w.tags
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            WHERE w.id = ANY(@ids) AND s.category <> 'removed'
            """,
            new { ids = ids.ToArray() }, cancellationToken: cancellationToken));
        return [.. rows.Select(ToText)];
    }

    public async Task<IReadOnlyList<Guid>> AllIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<Guid>(new CommandDefinition(
            "SELECT w.id FROM work_item w JOIN state s ON s.id = w.state_id WHERE s.category <> 'removed'", cancellationToken: cancellationToken))];
    }

    private static SearchSourceText ToText(ItemTextRow row)
    {
        var fields = new List<SearchFieldText>();
        if (!string.IsNullOrWhiteSpace(row.Title))
        {
            fields.Add(new SearchFieldText(SearchField.Title, row.Title.Trim()));
        }

        var tags = row.Tags.Cast<string>().Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (tags.Count > 0)
        {
            fields.Add(new SearchFieldText(SearchField.Tags, string.Join(' ', tags)));
        }

        var criteria = JsonSerializer.Deserialize<List<ChecklistItem>>(row.AcceptanceCriteria, PersistedJson.Options)!.Select(c => c.Text);
        var body = string.Join("\n\n", new[] { row.Description }.Concat(criteria).Append(row.ReproSteps).Where(p => !string.IsNullOrWhiteSpace(p)).Select(p => p.Trim()));
        if (body.Length > 0)
        {
            fields.Add(new SearchFieldText(SearchField.Body, body));
        }

        return new SearchSourceText(row.Id, row.ProjectId, row.Id, row.Title, fields);
    }

    // Tags is System.Array: Dapper hands a text[] to a record constructor that way.
    private sealed record ItemTextRow(Guid Id, Guid ProjectId, string Title, string Description, string AcceptanceCriteria, string ReproSteps, Array Tags);
}
