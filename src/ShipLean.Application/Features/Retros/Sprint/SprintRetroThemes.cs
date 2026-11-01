using ShipLean.Application.Features.Retros.Store;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Sprint;

// What the project's sprint series says about each theme, through RetroThemeSeries: mentions per sprint, where its fixes closed, and
// whether it recurs. Verdicts and recurrence use the whole series up to the viewed sprint; the strips show only its last window.
internal sealed class SprintRetroThemes
{
    private readonly int _windowStart;
    private readonly IReadOnlyDictionary<Guid, RetroThemeSeriesEntry> _entries;

    public SprintRetroThemes(IReadOnlyList<RetroSeriesSprint> series, int windowSize, IReadOnlyList<SprintMentionRow> mentions, IEnumerable<RetroSeriesClosed> closed, IReadOnlyDictionary<Guid, RetroThemeRefDto> themes)
    {
        _windowStart = Math.Max(0, series.Count - windowSize);
        _entries = RetroThemeSeries.Build(
            series,
            mentions.Select(m => (m.ThemeId, m.SprintId, Enum.Parse<RetroCategory>(m.Category, ignoreCase: true))),
            themes.ToDictionary(theme => theme.Key, theme => theme.Value.Tone),
            closed,
            new HashSet<Guid>());
    }

    // The mentions of the last window of the series, oldest first.
    public IReadOnlyList<int> Counts(Guid themeId) => _entries.TryGetValue(themeId, out var entry) ? [.. entry.Counts.Skip(_windowStart)] : [];

    public bool IsRecurring(Guid themeId) => _entries.TryGetValue(themeId, out var entry) && entry.IsRecurring;

    // The latest window index a fix on the theme closed in; null when none closed inside the window.
    public int? FixAt(Guid themeId)
        => _entries.TryGetValue(themeId, out var entry) && entry.Closed.Count > 0 && entry.Closed.Max(closed => closed.ClosingIndex) - _windowStart is var at and >= 0 ? at : null;

    // The verdict of a closed action item on the theme; null when the series can't date it.
    public RetroVerdict? Verdict(Guid themeId, Guid itemId)
        => _entries.TryGetValue(themeId, out var entry) ? entry.Closed.FirstOrDefault(closed => closed.ItemId == itemId)?.Result.Verdict : null;
}
