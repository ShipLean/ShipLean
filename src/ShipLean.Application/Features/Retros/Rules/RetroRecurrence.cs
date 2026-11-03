namespace ShipLean.Application.Features.Retros.Rules;

// How often a theme must be mentioned to recur: Mentions periods with a mention among the last Window periods.
public sealed record RetroRecurrence(int Window, int Mentions)
{
    public static RetroRecurrence Sprints { get; } = new(4, 3);

    public static RetroRecurrence Weeks { get; } = new(6, 3);
}
