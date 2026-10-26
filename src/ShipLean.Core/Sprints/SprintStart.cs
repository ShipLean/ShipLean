namespace ShipLean.Core.Sprints;

// Where a sprint begins: the day itself when it is a working day, else the next working day within a week. With no working
// day in that week the day itself stands.
public static class SprintStart
{
    // The length of the first sprint of a project, in calendar days.
    public const int FirstSprintDays = 14;

    public static DateOnly OnWorkingDay(DateOnly from, IReadOnlyCollection<int> workingDays)
    {
        var start = from;
        var limit = from.AddDays(7);
        while (!workingDays.Contains(IsoDay(start)) && start < limit)
        {
            start = start.AddDays(1);
        }
        return start;
    }

    private static int IsoDay(DateOnly day) => day.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)day.DayOfWeek;
}
