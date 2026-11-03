using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Search.Ranking.Fusion;

// A query that may be a work item key or part of one: "SHIP-435", "ship435", "IP-43" or "435". It needs a digit, so a word such
// as "ship" stays a text search. The part comes back the way keys are written: "SHIP-435", or "435" for a bare number.
public static partial class KeyPart
{
    public static bool TryParse(string? text, out string part)
    {
        part = "";
        if (text is null || PartPattern().Match(text) is not { Success: true } match || !int.TryParse(match.Groups[2].Value, out var number))
        {
            return false;
        }

        var letters = match.Groups[1].Value;
        part = letters.Length == 0 ? $"{number}" : $"{letters.ToUpperInvariant()}-{number}";
        return true;
    }

    [GeneratedRegex(@"^\s*(?:([A-Za-z]{1,4})-?)?(\d{1,9})\s*$", RegexOptions.CultureInvariant, matchTimeoutMilliseconds: 1000)]
    private static partial Regex PartPattern();
}
