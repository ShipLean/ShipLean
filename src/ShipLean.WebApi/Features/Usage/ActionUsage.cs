namespace ShipLean.WebApi.Features.Usage;

// One entry of ActionUsageMap. Track carries the event name (and the "board" property for a move); Untracked carries its reason.
public sealed record ActionUsage(ActionUsageKind Kind, string? EventName, string? Board, string? Reason)
{
    public static ActionUsage Track(string eventName, string? board = null) => new(ActionUsageKind.Track, eventName, board, null);

    public static ActionUsage SelfEmitted { get; } = new(ActionUsageKind.SelfEmitted, null, null, null);

    public static ActionUsage Untracked(string reason) => new(ActionUsageKind.Untracked, null, null, reason);
}
