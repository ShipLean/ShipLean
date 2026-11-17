using System.Globalization;

namespace ShipLean.Application.Features.InspectionMetrics.Cost;

// A token count as a short label: "950", "18.2k", "1.2M"; "—" for no number. A count that rounds up to 1000.0k reads as "1.0M".
public static class TokenLabel
{
    public const string None = "—";

    private const long Thousand = 1_000;
    private const long Million = 1_000_000;

    public static string Of(long? tokens)
    {
        if (tokens is not { } count)
        {
            return None;
        }
        if (count < Thousand)
        {
            return count.ToString(CultureInfo.InvariantCulture);
        }
        var thousands = Math.Round(count / (double)Thousand, 1, MidpointRounding.AwayFromZero);
        if (count < Million && thousands < Thousand)
        {
            return $"{thousands.ToString("0.0", CultureInfo.InvariantCulture)}k";
        }
        var millions = Math.Round(count / (double)Million, 1, MidpointRounding.AwayFromZero);
        return $"{millions.ToString("0.0", CultureInfo.InvariantCulture)}M";
    }
}
