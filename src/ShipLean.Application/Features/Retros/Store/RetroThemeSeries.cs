using ShipLean.Application.Features.Retros.Rules;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Store;

// The one place the recurring rule is applied, so the sprint view, Insights and Search agree.
//  - The series is the project's sprints oldest first; counts and verdicts run over all of it (readers trim what they show).
//  - A tone-bad theme counts mentions (wentBadly plus notInPlan), a tone-good theme counts wentWell points.
//  - A closed action's sprint is the one whose dates contain its completion date; a date between sprints belongs to the latest sprint
//    that started before it; a date before every sprint has no sprint, so the action gets no verdict.
//  - "Helped" is checked over every closed action on the theme, project-wide. Only tone-bad themes can recur.
//  - Status needs openThemes (themes with an open action item); callers that only want recurrence pass an empty set.
//  - Verdict maths is RetroRules (after = ended sprints only; before 0 is noEffect).
public static class RetroThemeSeries
{
    public static IReadOnlyDictionary<Guid, RetroThemeSeriesEntry> Build(
        IReadOnlyList<RetroSeriesSprint> series,
        IEnumerable<(Guid ThemeId, Guid SprintId, RetroCategory Category)> points,
        IReadOnlyDictionary<Guid, RetroTone> tones,
        IEnumerable<RetroSeriesClosed> closed,
        IReadOnlySet<Guid> openThemes)
    {
        var seriesIds = series.Select(sprint => sprint.Id).ToList();
        var lastEndedIndex = series.ToList().FindLastIndex(sprint => sprint.Ended);
        var pointsByTheme = points.ToLookup(point => point.ThemeId, point => (point.SprintId, point.Category));
        var closedByTheme = closed.ToLookup(action => action.ThemeId);
        var result = new Dictionary<Guid, RetroThemeSeriesEntry>();
        foreach (var (themeId, tone) in tones)
        {
            var typed = pointsByTheme[themeId].ToList();
            var counts = tone == RetroTone.Bad
                ? RetroRules.Mentions(seriesIds, typed)
                : [.. seriesIds.Select(id => typed.Count(point => point.SprintId == id && point.Category == RetroCategory.WentWell))];
            var verdicts = new List<RetroClosedVerdict>();
            foreach (var action in closedByTheme[themeId])
            {
                var index = ClosingIndex(series, DateOnly.FromDateTime(action.CompletedAt.UtcDateTime));
                if (index >= 0)
                {
                    verdicts.Add(new RetroClosedVerdict(action.ItemId, action.CompletedAt, index, RetroRules.Verdict(counts, index, lastEndedIndex)));
                }
            }

            var helped = verdicts.Any(verdict => verdict.Result.Verdict == RetroVerdict.Helped);
            var recurring = tone == RetroTone.Bad && RetroRules.IsRecurring(counts, helped);
            var status = RetroRules.Status(recurring, openThemes.Contains(themeId), helped);
            result[themeId] = new RetroThemeSeriesEntry(counts, verdicts, recurring, status);
        }

        return result;
    }

    // The sprint whose dates contain the day; a day between sprints belongs to the latest sprint that started before it. -1: before every sprint.
    public static int ClosingIndex(IReadOnlyList<RetroSeriesSprint> series, DateOnly day)
    {
        for (var i = 0; i < series.Count; i++)
        {
            if (series[i].Start <= day && day <= series[i].End)
            {
                return i;
            }
        }

        for (var i = series.Count - 1; i >= 0; i--)
        {
            if (series[i].Start <= day)
            {
                return i;
            }
        }

        return -1;
    }
}
