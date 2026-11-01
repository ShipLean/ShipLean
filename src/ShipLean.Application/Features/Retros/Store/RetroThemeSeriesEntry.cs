using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Store;

// What a series says about one theme. Counts run oldest sprint first, one per sprint of the series.
public sealed record RetroThemeSeriesEntry(IReadOnlyList<int> Counts, IReadOnlyList<RetroClosedVerdict> Closed, bool IsRecurring, RetroThemeStatus Status)
{
    public bool HasHelpedFix => Closed.Any(closed => closed.Result.Verdict == RetroVerdict.Helped);
}
