using System.Globalization;
using System.Text.RegularExpressions;

namespace ShipLean.Core.Sprints;

// The default name of a sprint under a project's naming convention. Pure: callers pass what is already taken.
public static partial class SprintNaming
{
    // The name for a sprint that starts on start and lasts lengthDays. nextNumber is the Number convention's candidate (see
    // NextNumber); YearQuarterNumber works its own out from the dates. A taken name bumps the number until one is free.
    public static string Name(SprintNamingConvention convention, DateOnly start, int lengthDays, Func<string, bool> taken, int nextNumber)
    {
        var (prefix, number) = convention == SprintNamingConvention.YearQuarterNumber
            ? ($"{start.Year} Q{(start.Month - 1) / 3 + 1} Sprint ", PlaceInQuarter(start, lengthDays))
            : ("Sprint ", nextNumber);
        while (taken($"{prefix}{number.ToString(CultureInfo.InvariantCulture)}"))
        {
            number++;
        }
        return $"{prefix}{number.ToString(CultureInfo.InvariantCulture)}";
    }

    // One more than the highest number any name ends in; the number of names plus one when none does.
    public static int NextNumber(IReadOnlyCollection<string> names)
    {
        var numbers = names
            .Select(name => TrailingNumber().Match(name))
            .Where(match => match.Success && int.TryParse(match.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n < int.MaxValue)
            .Select(match => int.Parse(match.Value, CultureInfo.InvariantCulture))
            .ToList();
        return numbers.Count == 0 ? names.Count + 1 : numbers.Max() + 1;
    }

    // The project.sprint_naming column's text.
    public static string ToColumn(SprintNamingConvention convention)
        => convention == SprintNamingConvention.YearQuarterNumber ? "yearQuarterNumber" : "number";

    public static SprintNamingConvention FromColumn(string? text)
        => text == "yearQuarterNumber" ? SprintNamingConvention.YearQuarterNumber : SprintNamingConvention.Number;

    // 1 + whole sprint lengths between the quarter's first day and the start: a start in the quarter's leftover days takes the next number.
    private static int PlaceInQuarter(DateOnly start, int lengthDays)
    {
        var quarterStart = new DateOnly(start.Year, (start.Month - 1) / 3 * 3 + 1, 1);
        return 1 + (start.DayNumber - quarterStart.DayNumber) / Math.Max(1, lengthDays);
    }

    [GeneratedRegex(@"\d+$")]
    private static partial Regex TrailingNumber();
}
