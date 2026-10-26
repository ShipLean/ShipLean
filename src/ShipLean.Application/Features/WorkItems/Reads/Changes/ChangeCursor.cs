using System.Buffers.Text;
using System.Globalization;
using System.Text;
using ShipLean.Contracts.Errors;

namespace ShipLean.Application.Features.WorkItems.Reads.Changes;

// Where a page stopped: the entry's time (UTC ticks), its kind rank and its tiebreak text. Entries sort by exactly this
// triple, so equal times page without gaps or repeats. Encoded as base64url of "ticks|kind|tie".
internal readonly record struct ChangeCursor(DateTime At, int Kind, string Tie)
{
    public string Encode() => Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{At.Ticks.ToString(CultureInfo.InvariantCulture)}|{Kind.ToString(CultureInfo.InvariantCulture)}|{Tie}"));

    public static ChangeCursor? Decode(string? cursor)
    {
        if (cursor is null)
        {
            return null;
        }

        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(cursor)).Split('|', 3);
            if (parts.Length == 3
                && long.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks <= DateTime.MaxValue.Ticks
                && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var kind)
                && kind is 0 or 1
                && parts[2].Length > 0)
            {
                return new ChangeCursor(new DateTime(ticks, DateTimeKind.Utc), kind, parts[2]);
            }
        }
        catch (FormatException)
        {
            // Falls through to the validation error below, the same as a cursor that decodes but is not ours.
        }

        throw ApiException.Validation("That cursor is not valid. Start again without one.", "cursor");
    }
}
