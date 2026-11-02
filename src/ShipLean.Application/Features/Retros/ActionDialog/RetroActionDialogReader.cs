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
    private const int WeeksShort = 6;
    private const int WeeksLong = 12;

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

        var weekMode = range is RetroRange.Weeks6 or RetroRange.Weeks12;
        var recurrence = weekMode ? RetroRecurrence.Weeks : RetroRecurrence.Sprints;
        Guid? Slot(InsightPointRow point) => weekMode ? RetroWeeks.IdOf(RetroWeeks.MondayOf(DateOnly.FromDateTime(point.CreatedAt.ToUniversalTime()))) : point.SprintId;

        var themeRefs = themes.ToDictionary(theme => theme.Id, theme => new RetroThemeRefDto(theme.Id, theme.Name, Enum.Parse<RetroTone>(theme.Tone, ignoreCase: true)));
        var closed = actions.Where(action => action.StateCategory == "completed").Select(action => action.WorkItemId).ToList();
        var completions = (await RetroSprintFacts.CompletionsAsync(connection, closed, cancellationToken)).ToDictionary(completion => completion.ItemId);
        var closedActions = actions
            .Where(action => action.ThemeId is { } themeId && themeRefs.ContainsKey(themeId) && completions.ContainsKey(action.WorkItemId))
            .Select(action => new RetroSeriesClosed(action.WorkItemId, action.ThemeId!.Value, completions[action.WorkItemId].CompletedAt))
            .ToList();

        var series = weekMode ? WeekSeries(points, closedActions, range, today, clock) : SprintSeries(sprints);
        var shown = weekMode ? [.. series.TakeLast(WeekCount(range))] : SprintShown(series, range);
        var offset = series.Count - shown.Count;
        var seriesStart = series.ToDictionary(column => column.Id, column => column.Start);

        var refs = sprints.ToDictionary(sprint => sprint.Id, sprint => sprint.ToRef());
        var pointsByTheme = points.Where(point => point.ThemeId is not null).ToLookup(point => point.ThemeId!.Value);

        var openThemes = actions.Where(action => action.ThemeId is not null && action.StateCategory != "completed").Select(action => action.ThemeId!.Value).ToHashSet();
        var seriesEntries = RetroThemeSeries.Build(
            [.. series.Select(column => column.Series)],
            points.Where(point => point.ThemeId is not null && Slot(point) is not null).Select(point => (point.ThemeId!.Value, Slot(point)!.Value, Enum.Parse<RetroCategory>(point.Category, ignoreCase: true))),
            themeRefs.ToDictionary(theme => theme.Key, theme => theme.Value.Tone),
            closedActions,
            openThemes,
            recurrence);

        // Closed before every live sprint: nothing to compare against, so no impact row (and no verdict).
        var outcomes = seriesEntries.Values.SelectMany(entry => entry.Closed).ToDictionary(verdict => verdict.ItemId);

        var actionRefs = actions.ToDictionary(action => action.WorkItemId, action => ToActionRef(action, refs));
        var themeRows = themes.Select(theme => BuildThemeRow(theme, themeRefs[theme.Id], pointsByTheme[theme.Id], actions, actionRefs, seriesEntries[theme.Id], Slot, seriesStart, offset, weekMode)).ToList();
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

        var impact = await ImpactAsync(actions, actionRefs, themeRefs, pointsByTheme, outcomes, seriesEntries, Slot, seriesStart, offset, bad, cancellationToken);
        var unthemed = points.Where(point => point.ThemeId is null && Enum.Parse<RetroCategory>(point.Category, ignoreCase: true) != RetroCategory.WentWell).ToList();
        var unthemedCounts = shown.Select(column => unthemed.Count(point => Slot(point) == column.Id)).ToList();
        var unthemedNoSprint = weekMode ? 0 : unthemed.Count(point => point.SprintId is null);

        return new RetroInsightsDto(RangeName(range), weekMode ? [] : [.. shown.Select(column => column.Ref!)], [.. bad.Select(row => row.Dto)], [.. good.Select(row => row.Dto)], actionRows, impact,
            weekMode ? RetroInsightsMode.Week : RetroInsightsMode.Sprint,
            [.. shown.Select(column => column.Dto)],
            new RetroUnthemedDto(unthemedCounts, unthemedNoSprint, unthemedCounts.Sum() + unthemedNoSprint),
            // IRetroMergeProposals has no read yet (T4 owns it): proposals are not listed until it has.
            []);
    }

    private async Task<IReadOnlyList<RetroImpactDto>> ImpactAsync(
        List<InsightActionRow> actions,
        Dictionary<Guid, RetroActionRefDto> actionRefs,
        Dictionary<Guid, RetroThemeRefDto> themeRefs,
        ILookup<Guid, InsightPointRow> pointsByTheme,
        Dictionary<Guid, RetroClosedVerdict> outcomes,
        IReadOnlyDictionary<Guid, RetroThemeSeriesEntry> seriesEntries,
        Func<InsightPointRow, Guid?> slot,
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
            if (!latest.ContainsKey(themeId) && Newest(RetroTone.Bad, pointsByTheme[themeId], slot, seriesStart) is { } mention)
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
        Func<InsightPointRow, Guid?> slot,
        Dictionary<Guid, DateOnly> seriesStart,
        int offset,
        bool weekMode)
    {
        var all = points.ToList();
        var noSprint = weekMode ? 0 : all.Count(point => point.SprintId is null && IsCounted(themeRef.Tone, point));
        var own = actions.Where(action => action.ThemeId == theme.Id).ToList();
        RetroVerdict? last = entry.Closed.OrderByDescending(outcome => outcome.CompletedAt).FirstOrDefault()?.Result.Verdict;
        var shown = entry.Counts.Skip(offset).ToList();
        var dto = new RetroThemeInsightDto(themeRef, shown, shown.Sum() + noSprint, LatestText(themeRef, all, slot, seriesStart), entry.Status, [.. own.OrderByDescending(action => action.CreatedAt).Select(action => actionRefs[action.WorkItemId])], last, noSprint);
        return new ThemeRow(themeRef, dto.Status, dto.Total, own.Count > 0, dto);
    }

    private static bool IsCounted(RetroTone tone, InsightPointRow point)
    {
        var category = Enum.Parse<RetroCategory>(point.Category, ignoreCase: true);
        return tone == RetroTone.Bad ? category is RetroCategory.WentBadly or RetroCategory.NotInPlan : category == RetroCategory.WentWell;
    }

    private static InsightPointRow? Newest(RetroTone tone, IEnumerable<InsightPointRow> points, Func<InsightPointRow, Guid?> slot, Dictionary<Guid, DateOnly> seriesStart)
        => points
            .Where(point => slot(point) is { } id && seriesStart.ContainsKey(id) && IsCounted(tone, point))
            .OrderByDescending(point => seriesStart[slot(point)!.Value])
            .ThenByDescending(point => point.CreatedAt)
            .FirstOrDefault();

    // A theme with no sprint points shows its newest no-sprint point instead.
    private static string? LatestText(RetroThemeRefDto theme, List<InsightPointRow> points, Func<InsightPointRow, Guid?> slot, Dictionary<Guid, DateOnly> seriesStart)
        => (Newest(theme.Tone, points, slot, seriesStart)
            ?? points.Where(point => slot(point) is null && IsCounted(theme.Tone, point)).OrderByDescending(point => point.CreatedAt).FirstOrDefault())?.Text;

    private static List<InsightColumn> SprintSeries(List<InsightSprint> sprints)
        => [.. sprints.Where(sprint => sprint.Phase == SprintPhase.Past).Concat(sprints.Where(sprint => sprint.Phase == SprintPhase.Current))
            .Select(sprint => new InsightColumn(
                new RetroSeriesSprint(sprint.Id, sprint.Start, sprint.End, sprint.Phase == SprintPhase.Past),
                new RetroInsightColumnDto(sprint.Id.ToString(), sprint.Name, RetroInsightColumnKind.Sprint, sprint.Phase == SprintPhase.Current),
                sprint.ToRef()))];

    private static int WeekCount(RetroRange range) => range == RetroRange.Weeks6 ? WeeksShort : WeeksLong;

    // From the week of the earliest point or closed action to this week, and at least as long as the range.
    private static List<InsightColumn> WeekSeries(List<InsightPointRow> points, List<RetroSeriesClosed> closed, RetroRange range, DateOnly today, TimeProvider clock)
    {
        var thisMonday = RetroWeeks.MondayOf(today);
        var days = points.Select(point => DateOnly.FromDateTime(point.CreatedAt.ToUniversalTime())).Concat(closed.Select(action => DateOnly.FromDateTime(action.CompletedAt.UtcDateTime))).ToList();
        var floor = thisMonday.AddDays(-7 * (WeekCount(range) - 1));
        var first = days.Count == 0 ? floor : new[] { RetroWeeks.MondayOf(days.Min()), floor }.Min();
        var result = new List<InsightColumn>();
        for (var monday = first; monday <= thisMonday; monday = monday.AddDays(7))
        {
            var sunday = monday.AddDays(6);
            var label = DisplayDate.ShortDay(new DateTimeOffset(monday.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), clock.GetUtcNow(), TimeZoneInfo.Utc);
            result.Add(new InsightColumn(new RetroSeriesSprint(RetroWeeks.IdOf(monday), monday, sunday, sunday < today), new RetroInsightColumnDto(monday.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), label, RetroInsightColumnKind.Week, monday == thisMonday), null));
        }

        return result;
    }

    // N columns including the current sprint, so the current one takes a slot from the ended sprints.
    private static List<InsightColumn> SprintShown(List<InsightColumn> series, RetroRange range)
    {
        if (range == RetroRange.All)
        {
            return series;
        }

        var window = range == RetroRange.Last3 ? ShortRange : LongRange;
        var ended = series.Count(column => column.Series.Ended);
        var current = series.Count - ended;
        return [.. series.Skip(Math.Max(0, ended - Math.Max(0, window - current)))];
    }

    private static RetroActionRefDto ToActionRef(InsightActionRow action, Dictionary<Guid, SprintRef> refs)
        => new(
            action.WorkItemId,
            action.Key,
            action.Title,
            new StateRef(action.StateId, action.StateName, Enum.Parse<StateCategory>(action.StateCategory, ignoreCase: true)),
            action.SprintId is { } sprintId && refs.TryGetValue(sprintId, out var sprint) ? sprint : null,
            action.Points,
            action.FixType is { } fixType ? Enum.Parse<RetroFixType>(fixType, ignoreCase: true) : null,
            action.PointCount);

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
        RetroRange.Weeks6 => "w6",
        RetroRange.Weeks12 => "w12",
        _ => "6",
    };

    private sealed record InsightSprintRow(Guid Id, string Name, string StartDate, string EndDate);


    private sealed record InsightPointRow(Guid Id, Guid RetroId, Guid? ThemeId, Guid? SprintId, string Category, string Text, DateTime CreatedAt);


    private sealed record InsightThemeRow(Guid Id, string Name, string Tone);


    private sealed record InsightActionRow(
        Guid WorkItemId, Guid? ThemeId, Guid? RaisedSprintId, string Key, string Title, decimal? Points, DateTime CreatedAt,
        Guid StateId, string StateName, string StateCategory, Guid? SprintId,
        Guid? OwnerId, string? OwnerName, string? OwnerInitials, string? OwnerColour, string? OwnerPhotoUrl,
        string? FixType, int PointCount);

    private sealed record InsightColumn(RetroSeriesSprint Series, RetroInsightColumnDto Dto, SprintRef? Ref)
    {
        public Guid Id => Series.Id;

        public DateOnly Start => Series.Start;
    }

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

        public string Name => _name;

        public DateOnly Start { get; }

        public DateOnly End { get; }

        public SprintPhase Phase { get; }

        public SprintRef ToRef() => new(Id, _name, Phase.ToStatus());
    }
}
