using System.Globalization;
using Dapper;
using Npgsql;
using ShipLean.Application.Features.Retros.Rules;
using ShipLean.Application.Features.Retros.Store;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.Core;

namespace ShipLean.Application.Features.Retros.Insights;

// Retrospectives > Insights. The series is every ended sprint plus the current one, oldest first. Counts, statuses and verdicts
// are all worked out over that whole series, so the range only changes which trailing sprints are shown. "After" counts only
// sprints that have ended (RetroRules.Verdict), so the current sprint's column is shown but never judged. A range of N shows N
// columns including the current sprint. ClosedAt is -1 when the action closed before the first shown sprint. An action closed
// before every live sprint of the project has no sprint to be placed in, so it gets no impact row (and no verdict).
public sealed class RetroInsightsReader(NpgsqlDataSource dataSource, TimeProvider clock, IRetroCards cards) : IRetroInsightsReader
{
    private const int ShortRange = 3;
    private const int LongRange = 6;

    public async Task<RetroInsightsDto> GetAsync(Guid projectId, RetroRange range, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var today = SprintLabels.Today(clock);
        var sprints = (await connection.QueryAsync<InsightSprintRow>(new CommandDefinition(RetroInsightsSql.Sprints, new { projectId }, cancellationToken: cancellationToken)))
            .Select(row => new InsightSprint(row, today))
            .ToList();
        var themes = (await connection.QueryAsync<InsightThemeRow>(new CommandDefinition(RetroInsightsSql.Themes, new { projectId }, cancellationToken: cancellationToken))).ToList();
        var points = (await connection.QueryAsync<InsightPointRow>(new CommandDefinition(RetroInsightsSql.Points, new { projectId }, cancellationToken: cancellationToken))).ToList();
        var actions = (await connection.QueryAsync<InsightActionRow>(new CommandDefinition(RetroInsightsSql.Actions, new { projectId }, cancellationToken: cancellationToken))).ToList();

        var ended = sprints.Where(sprint => sprint.Phase == SprintPhase.Past).ToList();
        var series = ended.Concat(sprints.Where(sprint => sprint.Phase == SprintPhase.Current)).ToList();
        var shown = Shown(ended, series, range);
        var offset = series.Count - shown.Count;
        var seriesStart = series.ToDictionary(sprint => sprint.Id, sprint => sprint.Start);

        var refs = sprints.ToDictionary(sprint => sprint.Id, sprint => sprint.ToRef());
        var themeRefs = themes.ToDictionary(theme => theme.Id, theme => new RetroThemeRefDto(theme.Id, theme.Name, Enum.Parse<RetroTone>(theme.Tone, ignoreCase: true)));
        var pointsByTheme = points.ToLookup(point => point.ThemeId);

        var closed = actions.Where(action => action.StateCategory == "completed").Select(action => action.WorkItemId).ToList();
        var completions = (await RetroSprintFacts.CompletionsAsync(connection, closed, cancellationToken)).ToDictionary(completion => completion.ItemId);
        var closedActions = actions
            .Where(action => action.ThemeId is { } themeId && themeRefs.ContainsKey(themeId) && completions.ContainsKey(action.WorkItemId))
            .Select(action => new RetroSeriesClosed(action.WorkItemId, action.ThemeId!.Value, completions[action.WorkItemId].CompletedAt));
        var openThemes = actions.Where(action => action.ThemeId is not null && action.StateCategory != "completed").Select(action => action.ThemeId!.Value).ToHashSet();
        var seriesEntries = RetroThemeSeries.Build(
            [.. series.Select(sprint => new RetroSeriesSprint(sprint.Id, sprint.Start, sprint.End, sprint.Phase == SprintPhase.Past))],
            points.Select(point => (point.ThemeId, point.SprintId, Enum.Parse<RetroCategory>(point.Category, ignoreCase: true))),
            themeRefs.ToDictionary(theme => theme.Key, theme => theme.Value.Tone),
            closedActions,
            openThemes);

        // Closed before every live sprint: nothing to compare against, so no impact row (and no verdict).
        var outcomes = seriesEntries.Values.SelectMany(entry => entry.Closed).ToDictionary(verdict => verdict.ItemId);

        var actionRefs = actions.ToDictionary(action => action.WorkItemId, action => ToActionRef(action, refs));
        var themeRows = themes.Select(theme => BuildThemeRow(theme, themeRefs[theme.Id], pointsByTheme[theme.Id], actions, actionRefs, seriesEntries[theme.Id], seriesStart, offset)).ToList();
        var visible = themeRows.Where(row => row.Total > 0 || row.HasActions).ToList();
        var bad = visible.Where(row => row.Theme.Tone == RetroTone.Bad).OrderBy(row => StatusOrder(row.Status)).ThenByDescending(row => row.Total).ThenBy(row => row.Theme.Name, StringComparer.Ordinal).ToList();
        var good = visible.Where(row => row.Theme.Tone == RetroTone.Good).OrderByDescending(row => row.Total).ThenBy(row => row.Theme.Name, StringComparer.Ordinal).ToList();

        var actionRows = actions
            .OrderBy(action => action.StateCategory == "completed")
            .ThenByDescending(action => action.CreatedAt)
            .Select(action => new RetroActionRowDto(
                actionRefs[action.WorkItemId],
                action.ThemeId is { } themeId && themeRefs.TryGetValue(themeId, out var theme) ? theme : null,
                action.RaisedSprintId is { } raised && refs.TryGetValue(raised, out var raisedRef) ? raisedRef : null,
                action.OwnerId is { } ownerId ? new UserRef(ownerId, action.OwnerName!, action.OwnerInitials!, action.OwnerColour!, action.OwnerPhotoUrl) : null,
                outcomes.TryGetValue(action.WorkItemId, out var outcome) ? outcome.Result.Verdict : null))
            .ToList();

        var impact = await ImpactAsync(actions, actionRefs, themeRefs, pointsByTheme, outcomes, seriesEntries, seriesStart, offset, bad, cancellationToken);
        return new RetroInsightsDto(RangeName(range), [.. shown.Select(sprint => sprint.ToRef())], [.. bad.Select(row => row.Dto)], [.. good.Select(row => row.Dto)], actionRows, impact);
    }

    private async Task<IReadOnlyList<RetroImpactDto>> ImpactAsync(
        List<InsightActionRow> actions,
        Dictionary<Guid, RetroActionRefDto> actionRefs,
        Dictionary<Guid, RetroThemeRefDto> themeRefs,
        ILookup<Guid, InsightPointRow> pointsByTheme,
        Dictionary<Guid, RetroClosedVerdict> outcomes,
        IReadOnlyDictionary<Guid, RetroThemeSeriesEntry> seriesEntries,
        Dictionary<Guid, DateOnly> seriesStart,
        int offset,
        List<ThemeRow> bad,
        CancellationToken cancellationToken)
    {
        var closedActions = actions.Where(action => outcomes.ContainsKey(action.WorkItemId)).OrderByDescending(action => outcomes[action.WorkItemId].CompletedAt).ToList();
        var latest = new Dictionary<Guid, InsightPointRow>();
        foreach (var action in closedActions.Where(action => outcomes[action.WorkItemId].Result.Verdict == RetroVerdict.NoEffect))
        {
            var themeId = action.ThemeId!.Value;
            if (!latest.ContainsKey(themeId) && Newest(RetroTone.Bad, pointsByTheme[themeId], seriesStart) is { } mention)
            {
                latest[themeId] = mention;
            }
        }

        var loaded = await cards.LoadAsync([.. latest.Values.Select(point => point.RetroId).Distinct()], cancellationToken);
        var recurring = bad.Where(row => row.Status == RetroThemeStatus.Recurring).Select(row => row.Theme.Id).ToHashSet();
        var result = new List<RetroImpactDto>();
        foreach (var action in closedActions)
        {
            var themeId = action.ThemeId!.Value;
            var theme = themeRefs[themeId];
            var outcome = outcomes[action.WorkItemId];
            RetroPointDto? mention = null;
            if (latest.TryGetValue(themeId, out var row) && outcome.Result.Verdict == RetroVerdict.NoEffect)
            {
                mention = loaded.SelectMany(card => card.Points).FirstOrDefault(point => point.Id == row.Id) is { } point ? point with { Recurring = recurring.Contains(themeId) } : null;
            }

            var shownCounts = seriesEntries[themeId].Counts.Skip(offset).ToList();
            result.Add(new RetroImpactDto(actionRefs[action.WorkItemId], theme, outcome.Result.Verdict, shownCounts, Math.Max(-1, outcome.ClosingIndex - offset), outcome.Result.BeforeAvg, outcome.Result.AfterAvg, outcome.Result.ChangePct, mention));
        }

        return result;
    }

    private static ThemeRow BuildThemeRow(
        InsightThemeRow theme,
        RetroThemeRefDto themeRef,
        IEnumerable<InsightPointRow> points,
        List<InsightActionRow> actions,
        Dictionary<Guid, RetroActionRefDto> actionRefs,
        RetroThemeSeriesEntry entry,
        Dictionary<Guid, DateOnly> seriesStart,
        int offset)
    {
        var own = actions.Where(action => action.ThemeId == theme.Id).ToList();
        RetroVerdict? last = entry.Closed.OrderByDescending(outcome => outcome.CompletedAt).FirstOrDefault()?.Result.Verdict;
        var shown = entry.Counts.Skip(offset).ToList();
        var dto = new RetroThemeInsightDto(themeRef, shown, shown.Sum(), LatestText(themeRef, points, seriesStart), entry.Status, [.. own.OrderByDescending(action => action.CreatedAt).Select(action => actionRefs[action.WorkItemId])], last);
        return new ThemeRow(themeRef, dto.Status, dto.Total, own.Count > 0, dto);
    }

    private static bool IsCounted(RetroTone tone, InsightPointRow point)
    {
        var category = Enum.Parse<RetroCategory>(point.Category, ignoreCase: true);
        return tone == RetroTone.Bad ? category is RetroCategory.WentBadly or RetroCategory.NotInPlan : category == RetroCategory.WentWell;
    }

    private static InsightPointRow? Newest(RetroTone tone, IEnumerable<InsightPointRow> points, Dictionary<Guid, DateOnly> seriesStart)
        => points
            .Where(point => seriesStart.ContainsKey(point.SprintId) && IsCounted(tone, point))
            .OrderByDescending(point => seriesStart[point.SprintId])
            .ThenByDescending(point => point.CreatedAt)
            .FirstOrDefault();

    private static string? LatestText(RetroThemeRefDto theme, IEnumerable<InsightPointRow> points, Dictionary<Guid, DateOnly> seriesStart)
        => Newest(theme.Tone, points, seriesStart)?.Text;

    private static List<InsightSprint> Shown(List<InsightSprint> ended, List<InsightSprint> series, RetroRange range)
    {
        if (range == RetroRange.All)
        {
            return series;
        }

        var window = range == RetroRange.Last3 ? ShortRange : LongRange;
        // N columns including the current sprint, so the current one takes a slot from the ended sprints.
        var current = series.Count - ended.Count;
        return [.. ended.TakeLast(Math.Max(0, window - current)), .. series.Skip(ended.Count)];
    }

    private static RetroActionRefDto ToActionRef(InsightActionRow action, Dictionary<Guid, SprintRef> refs)
        => new(
            action.WorkItemId,
            action.Key,
            action.Title,
            new StateRef(action.StateId, action.StateName, Enum.Parse<StateCategory>(action.StateCategory, ignoreCase: true)),
            action.SprintId is { } sprintId && refs.TryGetValue(sprintId, out var sprint) ? sprint : null,
            action.Points);

    private static int StatusOrder(RetroThemeStatus status) => status switch
    {
        RetroThemeStatus.Recurring => 0,
        RetroThemeStatus.BeingAddressed => 1,
        RetroThemeStatus.Resolved => 2,
        _ => 3,
    };

    private static string RangeName(RetroRange range) => range switch
    {
        RetroRange.Last3 => "3",
        RetroRange.All => "all",
        _ => "6",
    };

    private sealed record InsightSprintRow(Guid Id, string Name, string StartDate, string EndDate);


    private sealed record InsightPointRow(Guid Id, Guid RetroId, Guid ThemeId, Guid SprintId, string Category, string Text, DateTime CreatedAt);


    private sealed record InsightThemeRow(Guid Id, string Name, string Tone);


    private sealed record InsightActionRow(
        Guid WorkItemId, Guid? ThemeId, Guid? RaisedSprintId, string Key, string Title, decimal? Points, DateTime CreatedAt,
        Guid StateId, string StateName, string StateCategory, Guid? SprintId,
        Guid? OwnerId, string? OwnerName, string? OwnerInitials, string? OwnerColour, string? OwnerPhotoUrl);

    private sealed record ThemeRow(RetroThemeRefDto Theme, RetroThemeStatus Status, int Total, bool HasActions, RetroThemeInsightDto Dto);

    private sealed class InsightSprint
    {
        private readonly string _name;

        public InsightSprint(InsightSprintRow row, DateOnly today)
        {
            Id = row.Id;
            _name = row.Name;
            Start = DateOnly.ParseExact(row.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            End = DateOnly.ParseExact(row.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            Phase = SprintLabels.Phase(Start, End, today);
        }

        public Guid Id { get; }

        public DateOnly Start { get; }

        public DateOnly End { get; }

        public SprintPhase Phase { get; }

        public SprintRef ToRef() => new(Id, _name, Phase.ToStatus());
    }
}
