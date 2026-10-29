using ShipLean.Contracts.Holidays;
using ShipLean.Core.Sprints;

namespace ShipLean.Application.Sprints;

// A sprint's working days and the holidays on them, for the capacity maths. Shared by the Postgres store, the recompute and the fakes.
// Holidays lists every holiday that falls on a working weekday (two on one date are both listed, the date counts once in Days).
public sealed record CapacityCalendar(SprintDays Days, IReadOnlyList<PublicHolidayDto> Holidays)
{
    public int WorkingDays => Days.WorkingDays;

    public int DefaultDays => Days.DefaultDays;

    // What a member's typed days count as: null is the default, a value above W is clamped to W.
    public decimal Effective(decimal? typed) => Math.Min(typed ?? DefaultDays, WorkingDays);

    public static IReadOnlyList<int> ParseWorkingDays(string days)
        => [.. days.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(d => int.Parse(d, System.Globalization.CultureInfo.InvariantCulture))];

    public static async Task<CapacityCalendar> LoadAsync(
        IPublicHolidays holidays, string? country, IReadOnlyCollection<int> workingDays, DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        var found = country is null ? [] : await holidays.HolidaysAsync(country, start, end, cancellationToken);
        var onWorkingDays = found.Where(h => workingDays.Contains(h.Date.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)h.Date.DayOfWeek)).ToList();
        return new CapacityCalendar(SprintDays.Count(start, end, workingDays, onWorkingDays.Select(h => h.Date)), onWorkingDays);
    }
}
