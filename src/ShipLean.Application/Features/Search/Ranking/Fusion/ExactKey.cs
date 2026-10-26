using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Search.Ranking.Fusion;

// A query that is only a work item key: "SHIP-12", "ship-12" or "ship12". Project keys are 2-4 letters (ProjectKeys).
public static partial class ExactKey
{
    public static bool TryParse(string? text, out string key)
    {
        key = "";
        if (text is null || KeyPattern().Match(text) is not { Success: true } match || !int.TryParse(match.Groups[2].Value, out var number))
        {
            return false;
        }

        key = $"{match.Groups[1].Value.ToUpperInvariant()}-{number}";
        return true;
    }

    [GeneratedRegex(@"^\s*([A-Za-z]{2,4})-?(\d{1,9})\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex KeyPattern();
}
