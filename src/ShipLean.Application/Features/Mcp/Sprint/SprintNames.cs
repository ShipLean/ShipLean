using System.Globalization;
using System.Text.RegularExpressions;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Features.Mcp.Sprint;

public static partial class SprintNames
{
    // The name create_sprint gives a sprint starting on start and lasting lengthDays, under the project's convention. liveNames are
    // every live sprint's name (a taken name bumps the number); lastName and lastCount are the latest sprint's, null and 0 for none.
    // Number keeps the latest name's prefix ("Team A 41" -> "Team A 42"); a name with no trailing number becomes "Sprint <count + 1>".
    public static string Next(SprintNamingConvention convention, DateOnly start, int lengthDays, IReadOnlyCollection<string> liveNames, string? lastName, int lastCount)
    {
        var match = lastName is null ? null : TrailingNumber().Match(lastName.Trim());
        if (convention == SprintNamingConvention.Number && match is { Success: true })
        {
            var prefix = match.Groups[1].Value;
            var number = long.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture) + 1;
            while (liveNames.Contains($"{prefix}{number.ToString(CultureInfo.InvariantCulture)}"))
            {
                number++;
            }
            return $"{prefix}{number.ToString(CultureInfo.InvariantCulture)}";
        }

        return SprintNaming.Name(convention, start, lengthDays, liveNames.Contains, lastCount + 1);
    }

    [GeneratedRegex(@"^(.*?)(\d{1,9})$")]
    private static partial Regex TrailingNumber();
}
