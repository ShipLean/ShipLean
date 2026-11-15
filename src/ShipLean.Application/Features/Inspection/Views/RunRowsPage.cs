using System.Text.Json;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;

namespace ShipLean.Application.Features.Inspection.Views;

// The paged, errors-first rows of a run, for the MCP get_run tool.
public sealed record RunRowsPage(
    RunHeaderDto Header,
    bool LogOnly,
    string? Banner,
    bool Truncated,
    string? GrafanaUrl,
    bool ContentShown,
    int TotalRows,
    int ErrorRows,
    string? NextCursor,
    IReadOnlyList<RunRowsPageRow> Rows);

public sealed record RunRowsPageRow(int Turn, RunPageRowDto Row);

// An opaque cursor over the ordered rows: the filter it was made for and the sort key of the last row sent. The next page resumes after that
// key, so a row that appears while a run is still going cannot make a page repeat or skip rows.
public static class RunRowsCursor
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public sealed record Position(string Filter, int Rank, int Turn, long StartMs, string RowId);

    public static string Encode(string filter, RunRowsPageRow last)
        => Convert.ToBase64String(JsonSerializer.SerializeToUtf8Bytes(new Position(filter, RankOf(last), last.Turn, last.Row.StartMs, last.Row.Id)));

    // Null for the first page. A cursor made for another filter is an error, not a silent skip.
    public static Position? Decode(string? cursor, string filter)
    {
        if (cursor is null)
        {
            return null;
        }
        Position? position = null;
        try
        {
            position = JsonSerializer.Deserialize<Position>(Convert.FromBase64String(cursor));
        }
        catch (Exception e) when (e is FormatException or JsonException)
        {
            // Falls through to the validation error below.
        }
        if (position?.RowId is null || position.Filter is null)
        {
            throw ApiException.Validation("cursor is not one this tool returned.", "cursor");
        }
        if (position.Filter != filter)
        {
            throw ApiException.Validation($"cursor was made with filter={position.Filter}; use the same filter or start again without a cursor.", "cursor");
        }
        return position;
    }

    // Errors first, then the rest, each group by turn, start and row id.
    public static IReadOnlyList<RunRowsPageRow> Order(IEnumerable<RunRowsPageRow> rows)
        => [.. rows.OrderBy(RankOf).ThenBy(r => r.Turn).ThenBy(r => r.Row.StartMs).ThenBy(r => r.Row.Id, StringComparer.Ordinal)];

    // The rows after the cursor's position, in order.
    public static IEnumerable<RunRowsPageRow> After(IReadOnlyList<RunRowsPageRow> ordered, Position? position)
        => position is null ? ordered : ordered.Where(r => Compare(r, position) > 0);

    private static int RankOf(RunRowsPageRow row) => row.Row.IsError ? 0 : 1;

    private static int Compare(RunRowsPageRow row, Position at)
    {
        var byKey = RankOf(row).CompareTo(at.Rank);
        byKey = byKey != 0 ? byKey : row.Turn.CompareTo(at.Turn);
        byKey = byKey != 0 ? byKey : row.Row.StartMs.CompareTo(at.StartMs);
        return byKey != 0 ? byKey : string.CompareOrdinal(row.Row.Id, at.RowId);
    }
}
