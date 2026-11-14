using ShipLean.Application.Features.Timeline.Facts;
using ShipLean.Contracts.Views.Timeline;
using ShipLean.Core;

namespace ShipLean.Application.Features.Timeline.Build.Bars;

// The viewpoint one build shares: the clock reading (asOf), the viewer's zone, the axis start and who is who. Turns instants into
// the hour offsets and labels C1 sends, so no rule repeats the rounding.
internal sealed class TimelineLens(DateTimeOffset asOf, TimeZoneInfo zone, IReadOnlyDictionary<Guid, string> people)
{
    public DateTimeOffset AsOf { get; } = asOf;

    public TimeZoneInfo Zone { get; } = zone;

    // The axis start; set once the scale's first day is known.
    public DateTimeOffset Origin { get; set; }

    public static double Round(double value) => Math.Round(value, 1, MidpointRounding.AwayFromZero);

    public double HoursAt(DateTimeOffset at) => Round((at - Origin).TotalHours);

    public string DayTime(DateTimeOffset at) => DisplayDate.DayTime(at, Zone);

    public string DayLabel(DateTimeOffset at) => DisplayDate.DayLabel(at, Zone);

    public TimelinePersonDto? User(Guid? userId) =>
        userId is { } id ? new TimelinePersonDto(id, people.GetValueOrDefault(id, "Unknown"), false) : null;

    public static TimelinePersonDto Agent(string name) => new(null, name, true);

    // The agent when the move carries an agent name, else the user who made it.
    public TimelinePersonDto? Mover(TimelineMoveFact move) => move.AgentName is { } agent ? Agent(agent) : User(move.UserId);
}
