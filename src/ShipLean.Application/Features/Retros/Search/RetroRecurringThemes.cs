using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Rules;
using ShipLean.Application.Features.Retros.Store;
using ShipLean.Application.Sprints;
using ShipLean.Core;
using ShipLean.Contracts.Retros;

namespace ShipLean.Application.Features.Retros.Search;

// The themes of a project that recur, by the same rule as the sprint view and Insights: the series is every ended sprint plus the
// current one, oldest first; only tone-bad themes can recur; a theme with a helped fix (any closed action on it, project-wide) does not.
internal static class RetroRecurringThemes
{
    public static async Task<IReadOnlySet<Guid>> LoadAsync(NpgsqlConnection connection, Guid projectId, IReadOnlyList<SearchSprint> sprints, IReadOnlyList<SearchTheme> themes, CancellationToken cancellationToken)
    {
        var bad = themes.Where(theme => theme.Tone == RetroTone.Bad).Select(theme => theme.Id).ToHashSet();
        if (bad.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var series = sprints.Where(sprint => sprint.Phase is SprintPhase.Past or SprintPhase.Current).OrderBy(sprint => sprint.Phase == SprintPhase.Current).ToList();
        var seriesIds = series.Select(sprint => sprint.Id).ToList();
        var lastEndedIndex = series.Count(sprint => sprint.Phase == SprintPhase.Past) - 1;
        var mentions = (await connection.QueryAsync<MentionRow>(new CommandDefinition(RetroSearchSql.Mentions, new { projectId }, cancellationToken: cancellationToken)))
            .Where(row => bad.Contains(row.ThemeId))
            .ToLookup(row => row.ThemeId);
        var closed = (await connection.QueryAsync<ClosedRow>(new CommandDefinition(RetroSearchSql.ClosedActions, new { projectId }, cancellationToken: cancellationToken)))
            .Where(row => bad.Contains(row.ThemeId))
            .ToList();
        var completions = (await RetroSprintFacts.CompletionsAsync(connection, [.. closed.Select(row => row.ItemId)], cancellationToken)).ToDictionary(completion => completion.ItemId);

        var recurring = new HashSet<Guid>();
        foreach (var themeId in bad)
        {
            var counts = RetroRules.Mentions(seriesIds, mentions[themeId].Select(row => (row.SprintId, Enum.Parse<RetroCategory>(row.Category, ignoreCase: true))));
            var helped = closed
                .Where(row => row.ThemeId == themeId && completions.ContainsKey(row.ItemId))
                .Select(row => ClosingIndex(series, DateOnly.FromDateTime(completions[row.ItemId].CompletedAt.UtcDateTime)))
                .Where(index => index >= 0)
                .Any(index => RetroRules.Verdict(counts, index, lastEndedIndex).Verdict == RetroVerdict.Helped);
            if (RetroRules.IsRecurring(counts, helped))
            {
                recurring.Add(themeId);
            }
        }

        return recurring;
    }

    // The sprint whose dates contain the day; a day between sprints belongs to the latest sprint that started before it. -1: no sprint yet.
    private static int ClosingIndex(List<SearchSprint> series, DateOnly day)
    {
        var inside = series.FindIndex(sprint => sprint.Start <= day && day <= sprint.End);
        return inside >= 0 ? inside : series.FindLastIndex(sprint => sprint.Start <= day);
    }

    private sealed record MentionRow(Guid ThemeId, Guid SprintId, string Category);

    private sealed record ClosedRow(Guid ItemId, Guid ThemeId);
}
