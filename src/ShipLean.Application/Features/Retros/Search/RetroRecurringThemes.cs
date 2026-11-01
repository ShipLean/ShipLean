using Dapper;
using Npgsql;
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
        var bad = themes.Where(theme => theme.Tone == RetroTone.Bad).ToDictionary(theme => theme.Id, theme => theme.Tone);
        if (bad.Count == 0)
        {
            return new HashSet<Guid>();
        }

        var series = sprints
            .Where(sprint => sprint.Phase is SprintPhase.Past or SprintPhase.Current)
            .OrderBy(sprint => sprint.Phase == SprintPhase.Current)
            .Select(sprint => new RetroSeriesSprint(sprint.Id, sprint.Start, sprint.End, sprint.Phase == SprintPhase.Past))
            .ToList();
        var mentions = (await connection.QueryAsync<MentionRow>(new CommandDefinition(RetroSearchSql.Mentions, new { projectId }, cancellationToken: cancellationToken)))
            .Where(row => bad.ContainsKey(row.ThemeId))
            .Select(row => (row.ThemeId, row.SprintId, Enum.Parse<RetroCategory>(row.Category, ignoreCase: true)));
        var closed = (await connection.QueryAsync<ClosedRow>(new CommandDefinition(RetroSearchSql.ClosedActions, new { projectId }, cancellationToken: cancellationToken)))
            .Where(row => bad.ContainsKey(row.ThemeId))
            .ToList();
        var completions = (await RetroSprintFacts.CompletionsAsync(connection, [.. closed.Select(row => row.ItemId)], cancellationToken)).ToDictionary(completion => completion.ItemId);
        var closedActions = closed.Where(row => completions.ContainsKey(row.ItemId)).Select(row => new RetroSeriesClosed(row.ItemId, row.ThemeId, completions[row.ItemId].CompletedAt));

        return RetroThemeSeries.Build(series, mentions, bad, closedActions).Where(entry => entry.Value.IsRecurring).Select(entry => entry.Key).ToHashSet();
    }

    private sealed record MentionRow(Guid ThemeId, Guid SprintId, string Category);

    private sealed record ClosedRow(Guid ItemId, Guid ThemeId);
}
