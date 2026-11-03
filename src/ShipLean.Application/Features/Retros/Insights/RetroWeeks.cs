namespace ShipLean.Application.Features.Retros.Insights;

// Insights weeks run Monday to Sunday in UTC. A week's id is made from its Monday, so a week is the same column on every read.
internal static class RetroWeeks
{
    public static DateOnly MondayOf(DateOnly day) => day.AddDays(-(((int)day.DayOfWeek + 6) % 7));

    public static Guid IdOf(DateOnly monday) => new(monday.DayNumber, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
}
