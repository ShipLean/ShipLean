using Dapper;
using Npgsql;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index.Texts;

// A sprint's searchable text (docs/search.md): `sprint_name`, and `sprint_goal` when the goal is not blank. Archived sprints
// produce nothing, so the writer drops their rows. No title prefix.
public sealed class SprintTextSource(NpgsqlDataSource dataSource) : ISearchTextSource
{
    public SearchSourceKind Kind => SearchSourceKind.Sprint;

    public async Task<IReadOnlyList<SearchSourceText>> TextsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<SprintTextRow>(new CommandDefinition(
            "SELECT id, project_id AS projectId, name, goal FROM sprint WHERE id = ANY(@ids) AND archived_at IS NULL",
            new { ids = ids.ToArray() }, cancellationToken: cancellationToken));
        return [.. rows.Select(ToText)];
    }

    public async Task<IReadOnlyList<Guid>> AllIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<Guid>(new CommandDefinition("SELECT id FROM sprint WHERE archived_at IS NULL", cancellationToken: cancellationToken))];
    }

    private static SearchSourceText ToText(SprintTextRow row)
    {
        var fields = new List<SearchFieldText>();
        if (!string.IsNullOrWhiteSpace(row.Name))
        {
            fields.Add(new SearchFieldText(SearchField.SprintName, row.Name.Trim()));
        }

        if (!string.IsNullOrWhiteSpace(row.Goal))
        {
            fields.Add(new SearchFieldText(SearchField.SprintGoal, row.Goal.Trim()));
        }

        return new SearchSourceText(row.Id, row.ProjectId, null, null, fields);
    }

    private sealed record SprintTextRow(Guid Id, Guid ProjectId, string Name, string Goal);
}
