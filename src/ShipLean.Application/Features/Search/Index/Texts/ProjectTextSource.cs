using Dapper;
using Npgsql;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index.Texts;

// A project's searchable text (docs/search.md): one `project` field, "KEY Name", then the description on the next line, so a
// key-only query matches the project group. Every project qualifies (projects have no archived_at). Lexical only; no title prefix.
public sealed class ProjectTextSource(NpgsqlDataSource dataSource) : ISearchTextSource
{
    public SearchSourceKind Kind => SearchSourceKind.Project;

    public async Task<IReadOnlyList<SearchSourceText>> TextsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var rows = await connection.QueryAsync<ProjectTextRow>(new CommandDefinition(
            "SELECT id, key, name, description FROM project WHERE id = ANY(@ids)",
            new { ids = ids.ToArray() }, cancellationToken: cancellationToken));
        return [.. rows.Select(r => new SearchSourceText(r.Id, r.Id, null, null, [new SearchFieldText(SearchField.Project, ToText(r))]))];
    }

    public async Task<IReadOnlyList<Guid>> AllIdsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        return [.. await connection.QueryAsync<Guid>(new CommandDefinition("SELECT id FROM project", cancellationToken: cancellationToken))];
    }

    // A project with no description has no second line.
    private static string ToText(ProjectTextRow row)
    {
        var head = $"{row.Key} {row.Name.Trim()}";
        return string.IsNullOrWhiteSpace(row.Description) ? head : $"{head}\n{row.Description.Trim()}";
    }

    private sealed record ProjectTextRow(Guid Id, string Key, string Name, string Description);
}
