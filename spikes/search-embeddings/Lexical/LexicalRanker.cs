using System.Diagnostics;
using System.Text.RegularExpressions;
using Npgsql;
using SearchEmbeddings.Corpus;
using SearchEmbeddings.Hybrid;

namespace SearchEmbeddings.Lexical;

// Production's lexical leg (B3a LexicalItemCandidates) on a scratch table shaped like search_doc: tsv at the CB1 weights, the
// query websearch_to_tsquery english || simple, ts_rank_cd {0.1,0.2,0.4,1.0}, a title+tags list and a body+comment list (best
// row per item, top 100 each), fused by RRF k=60. A "prefix"-kind query is run in prefix mode, as the UI does while typing.
public sealed partial class LexicalRanker(bool orBody = false, bool orTitle = false, bool titleOnly = false) : IRanker
{
    private const int TopPerList = 100;
    private const int RrfK = 60;

    private const string Schema = """
        CREATE TABLE search_doc (
            id serial PRIMARY KEY, item_ref text NOT NULL, field text NOT NULL, chunk_no integer NOT NULL, text text NOT NULL,
            tsv tsvector GENERATED ALWAYS AS (
                setweight(to_tsvector('english'::regconfig, CASE WHEN field IN ('body', 'comment') THEN substr(text, strpos(text, E'\n') + 1) ELSE text END),
                    CASE field WHEN 'title' THEN 'A'::"char" WHEN 'tags' THEN 'B'::"char" WHEN 'body' THEN 'C'::"char" ELSE 'D'::"char" END)
                || setweight(to_tsvector('simple'::regconfig, CASE WHEN field IN ('body', 'comment') THEN substr(text, strpos(text, E'\n') + 1) ELSE text END),
                    CASE field WHEN 'title' THEN 'A'::"char" WHEN 'tags' THEN 'B'::"char" WHEN 'body' THEN 'C'::"char" ELSE 'D'::"char" END)
            ) STORED);
        CREATE INDEX ix_search_doc_tsv ON search_doc USING gin (tsv);
        """;

    private const string ListSql = """
        WITH q AS (SELECT (websearch_to_tsquery('english', @head) || websearch_to_tsquery('simple', @head)) AS query)
        SELECT item_ref FROM (
            SELECT DISTINCT ON (d.item_ref) d.item_ref, ts_rank_cd('{0.1, 0.2, 0.4, 1.0}', d.tsv, q.query)::float8 AS score
            FROM search_doc d CROSS JOIN q
            WHERE d.field = ANY(@fields) AND d.tsv @@ q.query
            ORDER BY d.item_ref, score DESC, d.chunk_no) best
        ORDER BY score DESC, item_ref LIMIT @top
        """;

    private const string ListPrefixSql = """
        WITH q AS (SELECT ((websearch_to_tsquery('english', @head) || websearch_to_tsquery('simple', @head)) && to_tsquery('simple', @last)) AS query)
        SELECT item_ref FROM (
            SELECT DISTINCT ON (d.item_ref) d.item_ref, ts_rank_cd('{0.1, 0.2, 0.4, 1.0}', d.tsv, q.query)::float8 AS score
            FROM search_doc d CROSS JOIN q
            WHERE d.field = ANY(@fields) AND d.tsv @@ q.query
            ORDER BY d.item_ref, score DESC, d.chunk_no) best
        ORDER BY score DESC, item_ref LIMIT @top
        """;

    // lexical-or (A5): the body+comment list ORs the words of websearch_to_tsquery ('a' & 'b' becomes 'a' | 'b'); the title+tags list
    // keeps AND. Ranking is still ts_rank_cd, so rows matching more of the words come first.
    private const string OrExpr = "replace(websearch_to_tsquery('english', @head)::text, ' & ', ' | ')::tsquery || replace(websearch_to_tsquery('simple', @head)::text, ' & ', ' | ')::tsquery";

    public string Label => (orBody, orTitle) switch { _ when titleOnly => orTitle ? "titles-or" : "titles-and", (true, _) => "lexical-or", (_, true) => "lexical-ortitle", _ => "lexical" };

    public Rankings Rank(IReadOnlyList<CorpusFile> corpus, IReadOnlyList<EvalQuery> queries, RankerOptions options)
    {
        using var database = new LexicalDatabase();
        using var connection = database.Open();
        using (var schema = new NpgsqlCommand(Schema, connection))
        {
            schema.ExecuteNonQuery();
        }
        using (var writer = connection.BeginBinaryImport("COPY search_doc (item_ref, field, chunk_no, text) FROM STDIN (FORMAT BINARY)"))
        {
            foreach (var row in SearchDocBuilder.Build(corpus, options.ChunkSize))
            {
                writer.StartRow();
                writer.Write(row.ItemRef);
                writer.Write(row.Field);
                writer.Write(row.ChunkNo);
                writer.Write(row.Text);
            }
            writer.Complete();
        }
        using (var analyze = new NpgsqlCommand("ANALYZE search_doc", connection))
        {
            analyze.ExecuteNonQuery();
        }

        var rankings = new Rankings(Label, [], []);
        foreach (var query in queries)
        {
            var clock = Stopwatch.StartNew();
            var (head, last) = SplitPrefix(query.Text, query.Kind == "prefix");
            var title = List(connection, head, last, ["title", "tags"], orTitle);
            var body = titleOnly ? [] : List(connection, head, last, ["body", "comment"], orBody);
            rankings.Queries[query.Id] = Rrf.Fuse([title, body], RrfK);
            rankings.LatencyMs[query.Id] = clock.Elapsed.TotalMilliseconds;
        }
        return rankings;
    }

    private static List<string> List(NpgsqlConnection connection, string head, string? last, string[] fields, bool or)
    {
        var sql = last is null ? ListSql : ListPrefixSql;
        using var command = new NpgsqlCommand(or ? sql.Replace("(websearch_to_tsquery('english', @head) || websearch_to_tsquery('simple', @head))", "(" + OrExpr + ")") : sql, connection);
        command.Parameters.AddWithValue("head", head);
        command.Parameters.AddWithValue("fields", fields);
        command.Parameters.AddWithValue("top", TopPerList);
        if (last is not null)
        {
            command.Parameters.AddWithValue("last", last);
        }
        using var reader = command.ExecuteReader();
        var refs = new List<string>();
        while (reader.Read())
        {
            refs.Add(reader.GetString(0));
        }
        return refs;
    }

    // With prefix set, the last word becomes "word:*" and the words before it are an ordinary web search, ANDed. A last word that
    // is not plain, or that follows an "or", is left to websearch_to_tsquery. Same rules as LexicalItemCandidates.SplitPrefix.
    private static (string Head, string? Last) SplitPrefix(string full, bool prefix)
    {
        var text = full.TrimEnd();
        if (!prefix || text.Length != full.Length)
        {
            return (full, null);
        }
        var match = LastWord().Match(text);
        if (!match.Success || string.Equals(match.Value, "or", StringComparison.OrdinalIgnoreCase) || OrBefore().IsMatch(text[..match.Index]))
        {
            return (full, null);
        }
        return (text[..match.Index], "'" + match.Value.Replace("\\", "\\\\").Replace("'", "''") + "':*");
    }

    [GeneratedRegex(@"(?<![\p{L}\p{M}\p{N}_\-""])[\p{L}\p{N}_][\p{L}\p{M}\p{N}_\-]*$")]
    private static partial Regex LastWord();

    [GeneratedRegex(@"(?<![\p{L}\p{M}\p{N}_\-""])or\s+$", RegexOptions.IgnoreCase)]
    private static partial Regex OrBefore();
}
