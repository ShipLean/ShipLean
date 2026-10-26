using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index.Pass;

// The SQL for "needs an embedding": an embedded field (SearchFields.IsEmbedded) with no vector, or a vector from another model.
internal static class PendingRows
{
    private const string Predicate =
        """
        field = ANY(@fields) AND (embedding IS NULL OR embedding_model IS DISTINCT FROM @modelId)
        """;

    // The claim is bounded by the pass start, so rows written while the pass runs wait for the next one; the count is not.
    public const string ClaimSql = $"SELECT id, text, text_hash AS TextHash FROM search_doc WHERE {Predicate} AND indexed_at <= @passStarted ORDER BY indexed_at, id LIMIT @batch FOR UPDATE SKIP LOCKED";

    public const string CountSql = $"SELECT count(*)::int FROM search_doc WHERE {Predicate}";

    public static object Parameters(string modelId, int batch, DateTime passStarted = default) => new
    {
        fields = Enum.GetValues<SearchField>().Where(SearchFields.IsEmbedded).Select(SearchDocColumns.Field).ToArray(),
        modelId,
        batch,
        passStarted,
    };
}
