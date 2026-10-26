using System.Text.RegularExpressions;

namespace ShipLean.Application.Features.Mcp.Sprint;

public static partial class SprintNames
{
    // "Sprint 25" -> "Sprint 26"; a name with no trailing number becomes "Sprint <count + 1>".
    public static string Next(string lastName, int count)
    {
        var match = TrailingNumber().Match(lastName.Trim());
        return match.Success
            ? $"{match.Groups[1].Value}{long.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture) + 1}"
            : $"Sprint {count + 1}";
    }

    [GeneratedRegex(@"^(.*?)(\d{1,9})$")]
    private static partial Regex TrailingNumber();
}
