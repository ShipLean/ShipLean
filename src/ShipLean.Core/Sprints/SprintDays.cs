namespace ShipLean.Core.Sprints;

// The sprint's working-day count: W is its dates on the project's working weekdays with holidays NOT removed (velocity assumes
// everyone is in on every one of them), Holidays are the holiday dates that fall on those days, and DefaultDays is W - H.
public sealed record SprintDays(int WorkingDays, IReadOnlyList<DateOnly> Holidays)
{
    public int DefaultDays => WorkingDays - Holidays.Count;

    // workingDays are ISO weekdays (Monday 1 .. Sunday 7). A holiday on a non-working weekday, outside the dates, or listed twice is not counted.
    public static SprintDays Count(DateOnly start, DateOnly end, IReadOnlyCollection<int> workingDays, IEnumerable<DateOnly> holidays)
    {
        var working = 0;
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            if (workingDays.Contains(IsoDay(day)))
            {
                working++;
            }
        }

        var onWorkingDays = holidays
            .Where(h => h >= start && h <= end && workingDays.Contains(IsoDay(h)))
            .Distinct()
            .Order()
            .ToList();
        return new SprintDays(working, onWorkingDays);
    }

    private static int IsoDay(DateOnly day) => day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;
}
