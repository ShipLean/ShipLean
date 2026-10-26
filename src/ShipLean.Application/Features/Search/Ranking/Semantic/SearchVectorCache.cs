using System.Collections.Concurrent;
using Dapper;
using Npgsql;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking.Semantic;

// The embedded rows of each project in memory, so a query is a dot product over arrays rather than a read of every vector.
// The key of a project's entry is read from search_doc on every query (one grouped count, no vectors) and the entry is
// reloaded when it differs: (max(indexed_at), count(*)) as docs/search.md says, plus the number of rows embedded with the
// loaded model, because the indexer may write an embedding without touching indexed_at, and a model swap changes which rows count.
// Only projects the caller is a member of are looked at, so a cached project is never reached through someone else's query.
public sealed class SearchVectorCache
{
    private static readonly string[] Fields = [.. new[] { SearchField.Title, SearchField.Body, SearchField.Comment, SearchField.Attachment }.Select(SearchDocColumns.Field)];

    private readonly ConcurrentDictionary<Guid, ProjectVectors> _projects = new();

    // One load of a project at a time, so concurrent cold queries share the result instead of each reading the vectors.
    private readonly ConcurrentDictionary<Guid, SemaphoreSlim> _loads = new();

    public async Task<IReadOnlyList<ProjectVectors>> ForAsync(
        NpgsqlConnection connection,
        Guid userId,
        IReadOnlyList<Guid> projectIds,
        string model,
        CancellationToken cancellationToken)
    {
        var keys = await connection.QueryAsync<KeyRow>(new CommandDefinition(
            """
            SELECT d.project_id AS ProjectId,
                   coalesce((extract(epoch FROM max(d.indexed_at)) * 1000000)::bigint, 0) AS MaxIndexedAt,
                   count(*)::int AS Rows,
                   (count(*) FILTER (WHERE d.embedding IS NOT NULL AND d.embedding_model = @model AND d.work_item_id IS NOT NULL AND d.field = ANY(@fields)))::int AS Embedded
            FROM search_doc d
            JOIN project_member m ON m.project_id = d.project_id AND m.user_id = @me AND m.archived_at IS NULL
            WHERE d.project_id = ANY(@projectIds)
            GROUP BY d.project_id
            """,
            new { me = userId, projectIds = projectIds.ToArray(), model, fields = Fields },
            cancellationToken: cancellationToken));

        var result = new List<ProjectVectors>();
        foreach (var key in keys)
        {
            if (IsCurrent(key, model, out var cached))
            {
                result.Add(cached);
                continue;
            }

            var gate = _loads.GetOrAdd(key.ProjectId, _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync(cancellationToken);
            try
            {
                // Another query may have loaded this key while this one waited. A failed load stores nothing, so the next query retries.
                result.Add(IsCurrent(key, model, out cached) ? cached : await LoadAsync(connection, key, model, cancellationToken));
            }
            finally
            {
                gate.Release();
            }
        }

        return result;
    }

    private bool IsCurrent(KeyRow key, string model, out ProjectVectors cached)
        => _projects.TryGetValue(key.ProjectId, out cached!)
            && cached.Model == model && cached.MaxIndexedAt == key.MaxIndexedAt && cached.Rows == key.Rows && cached.Embedded == key.Embedded;

    private async Task<ProjectVectors> LoadAsync(NpgsqlConnection connection, KeyRow key, string model, CancellationToken cancellationToken)
    {
        var rows = await connection.QueryAsync<RowRead>(new CommandDefinition(
            """
            SELECT id AS Id, work_item_id AS WorkItemId, field AS Field, embedding AS Vector
            FROM search_doc
            WHERE project_id = @projectId AND work_item_id IS NOT NULL AND field = ANY(@fields)
              AND embedding IS NOT NULL AND embedding_model = @model
            """,
            new { key.ProjectId, model, fields = Fields },
            cancellationToken: cancellationToken));
        // The key was read before the rows, so a write in between leaves the entry looking older than its rows
        // and the next query reloads it.
        var loaded = new ProjectVectors(key.ProjectId, model, key.MaxIndexedAt, key.Rows, key.Embedded,
            [.. rows.Select(r => new VectorRow(r.Id, r.WorkItemId, SearchDocColumns.ParseField(r.Field), (float[])r.Vector))]);
        _projects[key.ProjectId] = loaded;
        return loaded;
    }

    private sealed record KeyRow(Guid ProjectId, long MaxIndexedAt, int Rows, int Embedded);

    // Vector is an Array because that is the type Npgsql reports for real[], and Dapper matches the constructor on it.
    private sealed record RowRead(Guid Id, Guid WorkItemId, string Field, Array Vector);
}
