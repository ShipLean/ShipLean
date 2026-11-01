using ShipLean.Application.Features.Retros.Rules;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Sprint;

// What the window says about each theme: mentions per sprint, the window index where its fixes closed, and whether it recurs.
// A closed action item whose closing sprint is outside the window is left out, because the window can't date it.
internal sealed class SprintRetroThemes
{
    private readonly int _lastEndedIndex;
    private readonly Dictionary<Guid, IReadOnlyList<int>> _counts = [];
    private readonly Dictionary<Guid, List<int>> _fixes = [];
    private readonly Dictionary<Guid, bool> _recurring = [];

    // closedIndex maps a closed action item to the window index of the sprint it closed in.
    public SprintRetroThemes(IReadOnlyList<Guid> window, int lastEndedIndex, IReadOnlyList<SprintMentionRow> mentions, IReadOnlyList<SprintClosedActionRow> closed, IReadOnlyDictionary<Guid, int> closedIndex, IEnumerable<Guid> themeIds)
    {
        _lastEndedIndex = lastEndedIndex;
        foreach (var themeId in themeIds)
        {
            var counts = RetroRules.Mentions(window, mentions.Where(m => m.ThemeId == themeId).Select(m => (m.SprintId, Enum.Parse<RetroCategory>(m.Category, ignoreCase: true))));
            var fixes = closed.Where(c => c.ThemeId == themeId && closedIndex.ContainsKey(c.ItemId)).Select(c => closedIndex[c.ItemId]).ToList();
            _counts[themeId] = counts;
            _fixes[themeId] = fixes;
            var helped = fixes.Any(index => RetroRules.Verdict(counts, index, lastEndedIndex).Verdict == RetroVerdict.Helped);
            _recurring[themeId] = RetroRules.IsRecurring(counts, helped);
        }
    }

    public IReadOnlyList<int> Counts(Guid themeId) => _counts.TryGetValue(themeId, out var counts) ? counts : [];

    public bool IsRecurring(Guid themeId) => _recurring.TryGetValue(themeId, out var recurring) && recurring;

    // The latest window index a fix on the theme closed in.
    public int? FixAt(Guid themeId) => _fixes.TryGetValue(themeId, out var fixes) && fixes.Count > 0 ? fixes.Max() : null;

    public RetroVerdict Verdict(Guid themeId, int closedAt) => RetroRules.Verdict(Counts(themeId), closedAt, _lastEndedIndex).Verdict;
}
