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
// sprints that have ended (RetroRules.Verdict), so the current sprint's column is shown but never judged. ClosedAt is -1 when
// the action closed before the first shown sprint.
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
        var seriesIds = series.Select(sprint => sprint.Id).ToList();
        var lastEndedIndex = ended.Count - 1;

        var refs = sprints.ToDictionary(sprint => sprint.Id, sprint => sprint.ToRef());
        var themeRefs = themes.ToDictionary(theme => theme.Id, theme => new RetroThemeRefDto(theme.Id, theme.Name, Enum.Parse<RetroTone>(theme.Tone, ignoreCase: true)));
        var pointsByTheme = points.ToLookup(point => point.ThemeId);

        var closed = actions.Where(action => action.StateCategory == "completed").Select(action => action.WorkItemId).ToList();
        var completions = (await RetroSprintFacts.CompletionsAsync(connection, closed, cancellationToken)).ToDictionary(completion => completion.ItemId);

        var outcomes = new Dictionary<Guid, ClosedOutcome>();
        foreach (var action in actions.Where(action => action.ThemeId is { } themeId && themeRefs.ContainsKey(themeId) && completions.ContainsKey(action.WorkItemId)))
        {
            var completedAt = completions[action.WorkItemId].CompletedAt;
            var closedAt = ClosingIndex(series, DateOnly.FromDateTime(completedAt.UtcDateTime));
            if (closedAt < 0)
            {
                continue;
            }

            var counts = CountsOf(themeRefs[action.ThemeId!.Value], pointsByTheme[action.ThemeId.Value], seriesIds);
            outcomes[action.WorkItemId] = new ClosedOutcome(closedAt, completedAt, RetroRules.Verdict(counts, closedAt, lastEndedIndex));
        }

        var actionRefs = actions.ToDictionary(action => action.WorkItemId, action => ToActionRef(action, refs));
        var themeRows = themes.Select(theme => BuildThemeRow(theme, themeRefs[theme.Id], pointsByTheme[theme.Id], actions, actionRefs, outcomes, seriesIds, offset)).ToList();
        var visible = themeRows.Where(row => row.Total > 0 || row.HasActions).ToList();
        var bad = visible.Where(row => row.Theme.Tone == RetroTone.Bad).OrderBy(row => StatusOrder(row.Status)).ThenByDescending(row => row.Total).ThenBy(row => row.Theme.Name, StringComparer.Ordinal).ToList();
        var good = visible.Where(row => row.Theme.Tone == RetroTone.Good).OrderByDescending(row => row.Total).ThenBy(row => row.Theme.Name, StringComparer.Ordinal).ToList();

        var actionRows = actions
            .OrderBy(action => action.StateCategory is "completed" or "removed")
            .ThenByDescending(action => action.CreatedAt)
            .Select(action => new RetroActionRowDto(
                actionRefs[action.WorkItemId],
                action.ThemeId is { } themeId && themeRefs.TryGetValue(themeId, out var theme) ? theme : null,
                action.RaisedSprintId is { } raised && refs.TryGetValue(raised, out var raisedRef) ? raisedRef : null,
                action.OwnerId is { } ownerId ? new UserRef(ownerId, action.OwnerName!, action.OwnerInitials!, action.OwnerColour!, action.OwnerPhotoUrl) : null,
                outcomes.TryGetValue(action.WorkItemId, out var outcome) ? outcome.Result.Verdict : null))
            .ToList();

        var impact = await ImpactAsync(actions, actionRefs, themeRefs, pointsByTheme, outcomes, series, seriesIds, offset, bad, cancellationToken);
        return new RetroInsightsDto(RangeName(range), [.. shown.Select(sprint => sprint.ToRef())], [.. bad.Select(row => row.Dto)], [.. good.Select(row => row.Dto)], actionRows, impact);
    }

    private async Task<IReadOnlyList<RetroImpactDto>> ImpactAsync(
        List<InsightActionRow> actions,
        Dictionary<Guid, RetroActionRefDto> actionRefs,
        Dictionary<Guid, RetroThemeRefDto> themeRefs,
        ILookup<Guid, InsightPointRow> pointsByTheme,
        Dictionary<Guid, ClosedOutcome> outcomes,
        List<InsightSprint> series,
        List<Guid> seriesIds,
        int offset,
        List<ThemeRow> bad,
        CancellationToken cancellationToken)
    {
        var closedActions = actions.Where(action => outcomes.ContainsKey(action.WorkItemId)).OrderByDescending(action => outcomes[action.WorkItemId].CompletedAt).ToList();
        var latest = new Dictionary<Guid, InsightPointRow>();
        foreach (var action in closedActions.Where(action => outcomes[action.WorkItemId].Result.Verdict == RetroVerdict.NoEffect))
        {
            var themeId = action.ThemeId!.Value;
            if (!latest.ContainsKey(themeId) && LatestMention(pointsByTheme[themeId], series) is { } mention)
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

            var shownCounts = CountsOf(theme, pointsByTheme[themeId], seriesIds).Skip(offset).ToList();
            result.Add(new RetroImpactDto(actionRefs[action.WorkItemId], theme, outcome.Result.Verdict, shownCounts, Math.Max(-1, outcome.ClosedAt - offset), outcome.Result.BeforeAvg, outcome.Result.AfterAvg, outcome.Result.ChangePct, mention));
        }

        return result;
    }

    private static ThemeRow BuildThemeRow(
        InsightThemeRow theme,
        RetroThemeRefDto themeRef,
        IEnumerable<InsightPointRow> points,
        List<InsightActionRow> actions,
        Dictionary<Guid, RetroActionRefDto> actionRefs,
        Dictionary<Guid, ClosedOutcome> outcomes,
        List<Guid> seriesIds,
        int offset)
    {
        var counts = CountsOf(themeRef, points, seriesIds);
        var own = actions.Where(action => action.ThemeId == theme.Id).ToList();
        var verdicts = own.Where(action => outcomes.ContainsKey(action.WorkItemId)).Select(action => outcomes[action.WorkItemId]).ToList();
        var hasHelped = verdicts.Any(outcome => outcome.Result.Verdict == RetroVerdict.Helped);
        var hasOpen = own.Any(action => action.StateCategory is not ("completed" or "removed"));
        var recurring = themeRef.Tone == RetroTone.Bad && RetroRules.IsRecurring(counts, hasHelped);
        var status = RetroRules.Status(recurring, hasOpen, hasHelped);
        RetroVerdict? last = verdicts.OrderByDescending(outcome => outcome.CompletedAt).FirstOrDefault()?.Result.Verdict;
        var shown = counts.Skip(offset).ToList();
        var dto = new RetroThemeInsightDto(themeRef, shown, shown.Sum(), status, [.. own.OrderByDescending(action => action.CreatedAt).Select(action => actionRefs[action.WorkItemId])], last);
        return new ThemeRow(themeRef, dto.Status, dto.Total, own.Count > 0, dto);
    }

    // Bad themes count mentions (wentBadly plus notInPlan); good themes count wentWell points.
    private static IReadOnlyList<int> CountsOf(RetroThemeRefDto theme, IEnumerable<InsightPointRow> points, List<Guid> seriesIds)
    {
        var typed = points.Select(point => (point.SprintId, Category: Enum.Parse<RetroCategory>(point.Category, ignoreCase: true))).ToList();
        if (theme.Tone == RetroTone.Bad)
        {
            return RetroRules.Mentions(seriesIds, typed);
        }

        return [.. seriesIds.Select(id => typed.Count(point => point.SprintId == id && point.Category == RetroCategory.WentWell))];
    }

    private static InsightPointRow? LatestMention(IEnumerable<InsightPointRow> points, List<InsightSprint> series)
    {
        var start = series.ToDictionary(sprint => sprint.Id, sprint => sprint.Start);
        return points
            .Where(point => start.ContainsKey(point.SprintId) && Enum.Parse<RetroCategory>(point.Category, ignoreCase: true) is RetroCategory.WentBadly or RetroCategory.NotInPlan)
            .OrderByDescending(point => start[point.SprintId])
            .ThenByDescending(point => point.CreatedAt)
            .FirstOrDefault();
    }

    // The sprint whose dates contain the day; a day between sprints belongs to the latest sprint that started before it. -1: no sprint yet.
    private static int ClosingIndex(List<InsightSprint> series, DateOnly day)
    {
        var inside = series.FindIndex(sprint => sprint.Start <= day && day <= sprint.End);
        return inside >= 0 ? inside : series.FindLastIndex(sprint => sprint.Start <= day);
    }

    private static List<InsightSprint> Shown(List<InsightSprint> ended, List<InsightSprint> series, RetroRange range)
    {
        if (range == RetroRange.All)
        {
            return series;
        }

        var window = range == RetroRange.Last3 ? ShortRange : LongRange;
        return [.. ended.TakeLast(window), .. series.Skip(ended.Count)];
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

    private sealed record ClosedOutcome(int ClosedAt, DateTimeOffset CompletedAt, RetroVerdictResult Result);

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
