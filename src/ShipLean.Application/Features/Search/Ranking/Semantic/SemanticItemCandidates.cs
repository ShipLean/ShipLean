using System.Numerics;
using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking.Semantic;

// The semantic leg: cosine between the embedded query (CandidateQuery.Vector, set by the ranker) and the cached vectors of the
// caller's projects. Two lists, as in the lexical leg: title rows, and body, comment and attachment rows. An item's score is
// its best chunk in the list, chunks under SearchOptions.Cutoff are dropped, and the top SearchOptions.TopPerList items stay.
// The cache holds every member project's vectors unfiltered; closed, type, tag and criteria filters and removed items are applied
// in SQL to the items that scored, so a hidden item never reaches a list. Available is false without a query vector or when the
// embedder is lexical. PendingRows counts the matching items' embedded-field rows with no embedding from the loaded model.
public sealed class SemanticItemCandidates(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    ICriteriaEvaluator criteria,
    IEmbedder embedder,
    SearchVectorCache cache,
    SearchOptions options) : IItemCandidates
{
    private const int SnippetChars = 240;

    private static readonly string[] EmbeddedItemFields =
        [.. new[] { SearchField.Title, SearchField.Body, SearchField.Comment, SearchField.Attachment }.Where(SearchFields.IsEmbedded).Select(SearchDocColumns.Field)];

    public CandidateLeg Leg => CandidateLeg.Semantic;

    public async Task<CandidateLists> ListsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
    {
        if (query.Vector is null || embedder.Status.Mode != EmbedderMode.Semantic || embedder.ModelId is null)
        {
            return new CandidateLists(false, 0, []);
        }

        var model = embedder.ModelId.ToString();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var projects = await cache.ForAsync(connection, currentUser.Id, query.ProjectIds, model, cancellationToken);
        var (filters, parameters) = Filters(query, model);

        var title = Best(projects, query.Vector, titleList: true);
        var body = Best(projects, query.Vector, titleList: false);
        var pending = await connection.ExecuteScalarAsync<int>(new CommandDefinition(
            PendingSql(filters),
            Parameters(parameters, ("fields", EmbeddedItemFields)),
            cancellationToken: cancellationToken));

        var titleTop = await TopAsync(connection, title, filters, parameters, cancellationToken);
        var bodyTop = await TopAsync(connection, body, filters, parameters, cancellationToken);
        var texts = await TextsAsync(connection, [.. titleTop.Concat(bodyTop).Select(h => h.RowId)], cancellationToken);
        return new CandidateLists(
            true,
            pending,
            [new RankedList(RankedListName.TitleVector, ToHits(titleTop, texts)), new RankedList(RankedListName.BodyVector, ToHits(bodyTop, texts))]);
    }

    // The SQL text holds only fixed fragments; every value is a parameter.
    private static string EligibleSql(string filters)
        => $"""
            SELECT w.id
            FROM work_item w
            JOIN state s ON s.id = w.state_id
            JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @me AND m.archived_at IS NULL
            WHERE w.id = ANY(@ids) AND w.project_id = ANY(@projectIds) AND s.category <> 'removed'{filters}
            """;

    private static string PendingSql(string filters)
        => $"""
            SELECT count(*)::int
            FROM search_doc d
            JOIN work_item w ON w.id = d.work_item_id
            JOIN state s ON s.id = w.state_id
            JOIN project_member m ON m.project_id = w.project_id AND m.user_id = @me AND m.archived_at IS NULL
            WHERE d.project_id = ANY(@projectIds) AND d.field = ANY(@fields) AND s.category <> 'removed'
              AND (d.embedding IS NULL OR d.embedding_model IS DISTINCT FROM @model){filters}
            """;

    // The best row per item: the dot product of two unit vectors is their cosine. Rows of another size (a different model's) are skipped.
    private Dictionary<Guid, Scored> Best(IReadOnlyList<ProjectVectors> projects, float[] vector, bool titleList)
    {
        var best = new Dictionary<Guid, Scored>();
        foreach (var row in projects.SelectMany(p => p.Vectors))
        {
            if ((row.Field == SearchField.Title) != titleList || row.Vector.Length != vector.Length)
            {
                continue;
            }

            var score = Dot(vector, row.Vector);
            if (score >= options.Cutoff && (!best.TryGetValue(row.WorkItemId, out var current) || score > current.Score))
            {
                best[row.WorkItemId] = new Scored(row.WorkItemId, row.Id, score);
            }
        }

        return best;
    }

    // Best first, checking eligibility (the SQL filters) in chunks of descending score until TopPerList items qualify or the
    // candidates run out, so a filter that drops the best matches never leaves the list short while lower ones qualify.
    private async Task<List<Scored>> TopAsync(
        NpgsqlConnection connection,
        Dictionary<Guid, Scored> best,
        string filters,
        DynamicParameters parameters,
        CancellationToken cancellationToken)
    {
        var ordered = best.Values.OrderByDescending(s => s.Score).ThenBy(s => s.WorkItemId).ToList();
        var top = new List<Scored>();
        var chunk = Math.Max(1, options.TopPerList * 4);
        for (var start = 0; start < ordered.Count && top.Count < options.TopPerList; start += chunk)
        {
            var batch = ordered.Skip(start).Take(chunk).ToList();
            var eligible = (await connection.QueryAsync<Guid>(new CommandDefinition(
                EligibleSql(filters),
                Parameters(parameters, ("ids", batch.Select(s => s.WorkItemId).ToArray())),
                cancellationToken: cancellationToken))).ToHashSet();
            top.AddRange(batch.Where(s => eligible.Contains(s.WorkItemId)).Take(options.TopPerList - top.Count));
        }

        return top;
    }

    private static float Dot(float[] a, float[] b)
    {
        var sum = 0f;
        var i = 0;
        if (Vector.IsHardwareAccelerated)
        {
            var acc = Vector<float>.Zero;
            for (; i <= a.Length - Vector<float>.Count; i += Vector<float>.Count)
            {
                acc += new Vector<float>(a, i) * new Vector<float>(b, i);
            }

            sum = Vector.Sum(acc);
        }

        for (; i < a.Length; i++)
        {
            sum += a[i] * b[i];
        }

        return sum;
    }

        private (string Filters, DynamicParameters Parameters) Filters(CandidateQuery query, string model)
    {
        var parameters = new DynamicParameters(new { me = currentUser.Id, projectIds = query.ProjectIds.ToArray(), model });
        var filters = ItemFilterSql.Build(query, criteria, parameters);
        return (filters, parameters);
    }

    private static DynamicParameters Parameters(DynamicParameters shared, (string Name, object Value) extra)
    {
        var parameters = new DynamicParameters();
        parameters.AddDynamicParams(shared);
        parameters.Add(extra.Name, extra.Value);
        return parameters;
    }

    private static async Task<Dictionary<Guid, TextRow>> TextsAsync(NpgsqlConnection connection, Guid[] rowIds, CancellationToken cancellationToken)
    {
        if (rowIds.Length == 0)
        {
            return [];
        }

        var rows = await connection.QueryAsync<TextRow>(new CommandDefinition(
            """
            SELECT d.id AS Id, d.field AS Field, d.text AS Text, a.name AS Attachment
            FROM search_doc d
            LEFT JOIN attachment a ON d.source_kind = 'attachment' AND a.id = d.source_id
            WHERE d.id = ANY(@rowIds)
            """,
            new { rowIds },
            cancellationToken: cancellationToken));
        return rows.ToDictionary(r => r.Id);
    }

    // A row deleted since the cache was read has no text; its hit is dropped rather than shown blank.
    private static List<CandidateHit> ToHits(List<Scored> top, Dictionary<Guid, TextRow> texts)
        => [.. top.Where(s => texts.ContainsKey(s.RowId)).Select(s =>
        {
            var row = texts[s.RowId];
            var field = SearchDocColumns.ParseField(row.Field);
            return new CandidateHit(s.WorkItemId, field, s.Score, Snippet(field, row.Text), field == SearchField.Attachment ? row.Attachment : null);
        })];

    // Body, comment and attachment chunks start with the item title and a newline (docs/search.md): the snippet is what follows.
    private static string Snippet(SearchField field, string text)
    {
        var body = field == SearchField.Title ? text : text[(text.IndexOf('\n') + 1)..];
        body = body.Trim();
        if (body.Length <= SnippetChars)
        {
            return body;
        }

        var cut = body.LastIndexOf(' ', SnippetChars);
        var length = cut > SnippetChars / 2 ? cut : SnippetChars;
        if (length == SnippetChars && char.IsHighSurrogate(body[length - 1]))
        {
            length--;
        }

        return body[..length].TrimEnd() + "…";
    }

    private sealed record Scored(Guid WorkItemId, Guid RowId, double Score);

    private sealed record TextRow(Guid Id, string Field, string Text, string? Attachment);
}
