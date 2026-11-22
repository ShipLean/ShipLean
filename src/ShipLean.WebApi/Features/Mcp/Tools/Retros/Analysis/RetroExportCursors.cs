using System.Buffers.Text;
using System.Globalization;
using System.Text;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Retros.Analysis;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// The opaque `cursor` of export_retro_points: the logged time (UTC ticks) and id of the last point of a page, base64url. A cursor that does not read back is Validation naming `cursor`.
internal static class RetroExportCursors
{
    public static string Encode(RetroExportCursor cursor)
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{cursor.LoggedAt.UtcTicks.ToString(CultureInfo.InvariantCulture)}:{cursor.PointId:N}"));

    public static RetroExportCursor Decode(string text)
    {
        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(text.Trim())).Split(':');
            if (parts.Length == 2 && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks) && Guid.TryParseExact(parts[1], "N", out var id)
                && ticks <= DateTime.MaxValue.Ticks)
            {
                return new RetroExportCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
            }
        }
        catch (FormatException)
        {
            // Falls through to the refusal below.
        }

        throw ApiException.Validation("That cursor is not valid. Pass the next_cursor of the previous page, or leave it out for the first page.", "cursor");
    }
}
