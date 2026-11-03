using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Sprint;

// Groups the sprint's points into the category columns, in the display order (RetroCategories.Display). ToDecide counts the column's undecided points.
// Groups the sprint's points into the category columns. A column's groups run largest first, then by theme name, with Unthemed last.
// Inside a group the feature points come first, then the oldest.
internal static class SprintRetroColumns
{
    public static IReadOnlyList<RetroCategoryColumnDto> Build(IReadOnlyList<RetroCardDto> cards, IReadOnlyDictionary<Guid, DateTimeOffset> pointDates)
    {
        var points = cards.SelectMany((card, cardIndex) => card.Points.Select((point, index) => (Point: point, CardIndex: cardIndex, Index: index))).ToList();
        return [.. RetroCategories.Display.Select(category => Column(category, points.Where(p => p.Point.Category == category).ToList(), pointDates))];
    }

    private static RetroCategoryColumnDto Column(RetroCategory category, List<(RetroPointDto Point, int CardIndex, int Index)> points, IReadOnlyDictionary<Guid, DateTimeOffset> pointDates)
    {
        var groups = points
            .GroupBy(p => p.Point.Theme?.Id)
            .OrderBy(group => group.Key is null ? 1 : 0)
            .ThenByDescending(group => group.Count())
            .ThenBy(group => group.First().Point.Theme?.Name, StringComparer.OrdinalIgnoreCase)
            .Select(group => new RetroThemeGroupDto(
                group.First().Point.Theme,
                group.First().Point.Recurring,
                [.. group
                    .OrderBy(p => p.Point.IsFeature ? 0 : 1)
                    .ThenBy(p => pointDates.TryGetValue(p.Point.Id, out var at) ? at : DateTimeOffset.MaxValue)
                    .ThenBy(p => p.CardIndex)
                    .ThenBy(p => p.Index)
                    .Select(p => p.Point)]));
        return new RetroCategoryColumnDto(category, [.. groups], points.Count(p => p.Point.Decision == RetroDecision.Undecided));
    }
}
