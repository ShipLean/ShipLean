using ShipLean.Application.Features.Retros.Rules;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Sprint;

// What the project's sprint series says about each theme: mentions per sprint, the series index where its fixes closed, and whether
// it recurs. Verdicts and recurrence use the whole series up to the viewed sprint; the strips show only its last window.
// A closed action item whose closing sprint is outside the series is left out, because the series can't date it.
internal sealed class SprintRetroThemes
{
    private readonly int _lastEndedIndex;
    private readonly int _windowStart;
    private readonly Dictionary<Guid, IReadOnlyList<int>> _counts = [];
    private readonly Dictionary<Guid, List<int>> _fixes = [];
    private readonly Dictionary<Guid, bool> _recurring = [];

    // closedIndex maps a closed action item to the series index of the sprint it closed in. Only tone-bad themes can recur.
    public SprintRetroThemes(IReadOnlyList<Guid> series, int windowSize, int lastEndedIndex, IReadOnlyList<SprintMentionRow> mentions, IReadOnlyList<SprintClosedActionRow> closed, IReadOnlyDictionary<Guid, int> closedIndex, IReadOnlyDictionary<Guid, RetroThemeRefDto> themes)
    {
        _lastEndedIndex = lastEndedIndex;
        _windowStart = Math.Max(0, series.Count - windowSize);
        foreach (var (themeId, theme) in themes)
        {
            var counts = RetroRules.Mentions(series, mentions.Where(m => m.ThemeId == themeId).Select(m => (m.SprintId, Enum.Parse<RetroCategory>(m.Category, ignoreCase: true))));
            var fixes = closed.Where(c => c.ThemeId == themeId && closedIndex.ContainsKey(c.ItemId)).Select(c => closedIndex[c.ItemId]).ToList();
            _counts[themeId] = counts;
            _fixes[themeId] = fixes;
            var helped = fixes.Any(index => RetroRules.Verdict(counts, index, lastEndedIndex).Verdict == RetroVerdict.Helped);
            _recurring[themeId] = theme.Tone == RetroTone.Bad && RetroRules.IsRecurring(counts, helped);
        }
    }

    // The mentions of the last window of the series, oldest first.
    public IReadOnlyList<int> Counts(Guid themeId) => _counts.TryGetValue(themeId, out var counts) ? [.. counts.Skip(_windowStart)] : [];

    public bool IsRecurring(Guid themeId) => _recurring.TryGetValue(themeId, out var recurring) && recurring;

    // The latest window index a fix on the theme closed in; null when none closed inside the window.
    public int? FixAt(Guid themeId)
        => _fixes.TryGetValue(themeId, out var fixes) && fixes.Count > 0 && fixes.Max() - _windowStart is var at and >= 0 ? at : null;

    public RetroVerdict Verdict(Guid themeId, int closedAt) => _counts.TryGetValue(themeId, out var counts) ? RetroRules.Verdict(counts, closedAt, _lastEndedIndex).Verdict : RetroVerdict.TooEarly;
}
