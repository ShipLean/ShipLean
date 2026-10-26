using Dapper;
using Npgsql;
using ShipLean.Contracts;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Ranking.Lexical;

// The lexical leg: search_doc.tsv matched with websearch_to_tsquery on both configs (docs/search.md) and ranked with ts_rank_cd
// at the CB1 weights. Two lists: the title (and tags) rows, and the body, comment and attachment rows. One hit per item, its best row.
// Access, removed items, types, tags, closed and criteria are all in the SQL, so a hidden item never reaches a list.
public sealed class LexicalItemCandidates(
    NpgsqlDataSource dataSource,
    ICurrentUser currentUser,
    ICriteriaEvaluator criteria,
    SearchOptions options) : IItemCandidates
{
    // ts_rank_cd takes the weights as {D, C, B, A}: the weights of CB1 (title and project A, tags B, body and goal C, comment and name D).
    private const string Weights = "'{0.1, 0.2, 0.4, 1.0}'";

    private static readonly SearchField[] TitleFields = [SearchField.Title, SearchField.Tags];
    private static readonly SearchField[] BodyFields = [SearchField.Body, SearchField.Comment, SearchField.Attachment];

    public CandidateLeg Leg => CandidateLeg.Lexical;

    public async Task<CandidateLists> ListsAsync(CandidateQuery query, CancellationToken cancellationToken = default)
    {
        var (head, last) = SearchTsQuery.Split(query.Text, query.Prefix);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var title = await ListAsync(connection, query, head, last, false, TitleFields, cancellationToken);
        var body = await ListAsync(connection, query, head, last, false, BodyFields, cancellationToken);
        List<RankedList> lists = [new(RankedListName.TitleLexical, title), new(RankedListName.BodyLexical, body)];

        // Without a query vector the semantic leg cannot answer, so the body, comment and attachment words are also matched with "or": a
        // paraphrase that shares one word is found, below the items that have all of them. Titles stay AND (A5: an OR title list only added
        // noise and cost 2x the p95). With a vector the lexical leg stays AND.
        if (query.Vector is null && SearchTsQuery.AnyWords(head, last) is { } anyQuery)
        {
            lists.Add(new(RankedListName.BodyLexicalAny, await ListAsync(connection, query, anyQuery.Head, anyQuery.Last, true, BodyFields, cancellationToken)));
        }

        return new CandidateLists(true, 0, lists);
    }

    private async Task<IReadOnlyList<CandidateHit>> ListAsync(
        NpgsqlConnection connection,
        CandidateQuery query,
        string head,
        string? last,
        bool any,
        SearchField[] fields,
        CancellationToken cancellationToken)
    {
        var parameters = new DynamicParameters(new
        {
            me = currentUser.Id,
            projectIds = query.ProjectIds.ToArray(),
            fields = fields.Select(SearchDocColumns.Field).ToArray(),
            head,
            last,
            top = options.TopPerList,
        });

        var filters = ItemFilterSql.Build(query, criteria, parameters);

        var text = Sql(SearchTsQuery.Expression(last is not null, any), filters);
        var rows = await connection.QueryAsync<HitRow>(new CommandDefinition(
            text,
            parameters,
            cancellationToken: cancellationToken));
        return [.. rows.Select(r => new CandidateHit(r.WorkItemId, SearchDocColumns.ParseField(r.Field), r.Score, r.Snippet, r.Attachment))];
    }

    // The SQL text holds only fixed fragments; every value is a parameter.
    private static string Sql(string tsquery, string filters)
        => $"""
            WITH q AS (SELECT {tsquery} AS query),
            best AS (
                SELECT DISTINCT ON (d.work_item_id) d.work_item_id, d.id, d.field, d.source_id, d.source_kind,
                       ts_rank_cd({Weights}, d.tsv, q.query)::float8 AS score
                FROM search_doc d
                CROSS JOIN q
                JOIN project_member m ON m.project_id = d.project_id AND m.user_id = @me AND m.archived_at IS NULL
                JOIN work_item w ON w.id = d.work_item_id
                JOIN state s ON s.id = w.state_id
                WHERE d.project_id = ANY(@projectIds) AND d.field = ANY(@fields) AND d.tsv @@ q.query
                  AND s.category <> 'removed'{filters}
                ORDER BY d.work_item_id, score DESC, d.chunk_no),
            top AS (SELECT * FROM best ORDER BY score DESC, work_item_id LIMIT @top)
            SELECT t.work_item_id AS WorkItemId, t.field AS Field, t.score AS Score,
                   ts_headline('english',
                       CASE WHEN t.field IN ('body', 'comment', 'attachment') THEN substr(d.text, strpos(d.text, E'\n') + 1) ELSE d.text END,
                       q.query, 'StartSel="", StopSel="", MaxWords=30, MinWords=12, MaxFragments=1') AS Snippet,
                   a.name AS Attachment
            FROM top t
            CROSS JOIN q
            JOIN search_doc d ON d.id = t.id
            LEFT JOIN attachment a ON t.source_kind = 'attachment' AND a.id = t.source_id
            ORDER BY t.score DESC, t.work_item_id
            """;

    private sealed record HitRow(Guid WorkItemId, string Field, double Score, string Snippet, string? Attachment);
}
