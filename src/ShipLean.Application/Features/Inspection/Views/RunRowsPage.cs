using System.Globalization;
using System.Text;
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

// An opaque cursor over the ordered rows: the offset of the next row.
public static class RunRowsCursor
{
    public const int DefaultLimit = 50;
    public const int MaxLimit = 200;

    public static string Encode(int offset) => Convert.ToBase64String(Encoding.UTF8.GetBytes("o" + offset.ToString(CultureInfo.InvariantCulture)));

    public static int Decode(string? cursor)
    {
        if (cursor is null)
        {
            return 0;
        }
        try
        {
            var text = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            if (text.StartsWith('o') && int.TryParse(text.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture, out var offset))
            {
                return offset;
            }
        }
        catch (FormatException)
        {
            // Falls through to the validation error below.
        }
        throw ApiException.Validation("cursor is not one this tool returned.", "cursor");
    }

    // Errors first, then the rest, each group in run order.
    public static IReadOnlyList<RunRowsPageRow> Order(IEnumerable<RunRowsPageRow> rows)
    {
        var all = rows.ToList();
        return [.. all.Where(r => r.Row.IsError), .. all.Where(r => !r.Row.IsError)];
    }
}
