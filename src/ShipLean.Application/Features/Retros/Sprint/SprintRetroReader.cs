using System.Globalization;
using Npgsql;
using ShipLean.Application.Features.Retros.Rules;
using ShipLean.Application.Features.Retros.Store;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.Core;

namespace ShipLean.Application.Features.Retros.Sprint;

// The sprint Retro tab: every live retro attributed to the sprint (retrospective.sprint_id), grouped by theme, with coverage,
// action items and the recurring themes over the last six sprints ending with this one.
public sealed class SprintRetroReader(NpgsqlDataSource dataSource, IRetroCards retroCards, TimeProvider clock) : ISprintRetroReader
{
    public async Task<SprintRetroDto> GetAsync(Guid sprintId, CancellationToken cancellationToken = default)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var today = SprintLabels.Today(clock);
        var sprintRow = await SprintRetroStore.SprintAsync(connection, sprintId, cancellationToken);
        var series = await SprintRetroStore.SeriesAsync(connection, sprintRow, cancellationToken);
        var seriesIds = series.Select(row => row.Id).ToList();
        var windowRows = series.Skip(Math.Max(0, series.Count - SprintRetroStore.WindowSize)).ToList();
        var windowIds = windowRows.Select(row => row.Id).ToHashSet();
        var lastEnded = series.ToList().FindLastIndex(row => Day(row.End) < today);

        var retroIds = await SprintRetroStore.RetroIdsAsync(connection, sprintId, cancellationToken);
        var loaded = await retroCards.LoadAsync(retroIds, cancellationToken);
        var pointDates = await SprintRetroStore.PointDatesAsync(connection, retroIds, cancellationToken);

        var sprintThemes = loaded.SelectMany(card => card.Points).Where(point => point.Theme is not null).Select(point => point.Theme!.Id).Distinct().ToList();
        var mentions = await SprintRetroStore.MentionsAsync(connection, seriesIds, cancellationToken);
        var actions = await SprintRetroStore.ActionsAsync(connection, sprintRow, sprintThemes, cancellationToken);
        var refs = new Dictionary<Guid, RetroThemeRefDto>();
        foreach (var mention in mentions.Where(m => windowIds.Contains(m.SprintId)))
        {
            refs.TryAdd(mention.ThemeId, new RetroThemeRefDto(mention.ThemeId, mention.ThemeName, Enum.Parse<RetroTone>(mention.ThemeTone, ignoreCase: true)));
        }
        foreach (var theme in loaded.SelectMany(card => card.Points).Select(point => point.Theme).OfType<RetroThemeRefDto>())
        {
            refs.TryAdd(theme.Id, theme);
        }
        foreach (var action in actions.Where(a => a.ThemeId is not null))
        {
            refs.TryAdd(action.ThemeId!.Value, new RetroThemeRefDto(action.ThemeId.Value, action.ThemeName!, Enum.Parse<RetroTone>(action.ThemeTone!, ignoreCase: true)));
        }
        var closed = await SprintRetroStore.ClosedActionsAsync(connection, sprintRow.ProjectId, [.. refs.Keys], cancellationToken);
        var completions = await RetroSprintFacts.CompletionsAsync(connection, [.. closed.Select(c => c.ItemId).Distinct()], cancellationToken);
        var closedIndex = completions
            .Where(c => c.SprintId is { } id && seriesIds.Contains(id))
            .ToDictionary(c => c.ItemId, c => seriesIds.IndexOf(c.SprintId!.Value));
        var themes = new SprintRetroThemes(seriesIds, SprintRetroStore.WindowSize, lastEnded, mentions, closed, closedIndex, refs);

        var cards = loaded.Select(card => card with { Points = [.. card.Points.Select(point => point with { Recurring = point.Theme is { } t && themes.IsRecurring(t.Id) })] }).ToList();
        var coverageRows = await SprintRetroStore.CoverageAsync(connection, sprintId, cancellationToken);
        var coverage = coverageRows
            .Select(row => new RetroCoverageRowDto(
                new RetroItemRefDto(row.Id, row.Key, ToTypeRef(row.Type), row.Title),
                RetroRules.Coverage(row.IsFeature, row.LastChildCompleted, row.HasRetro)))
            .ToList();
        var recurring = RecurringStrips(refs, themes);
        var actionRows = actions.Select(action => ToActionRow(action, today, themes, closedIndex)).ToList();

        return new SprintRetroDto(
            ToSprint(sprintRow, today),
            new RetroKpisDto(
                coverage.Count(row => row.Status == RetroCoverage.Done),
                coverage.Count(row => row.Status != RetroCoverage.Waiting),
                recurring.Count,
                actions.Count(action => action.RaisedId == sprintId)),
            [.. windowRows.Select(row => ToSprint(row, today))],
            SprintRetroColumns.Build(cards, pointDates),
            cards,
            coverage,
            actionRows,
            recurring);
    }

    private static List<RetroMentionStripDto> RecurringStrips(IReadOnlyDictionary<Guid, RetroThemeRefDto> refs, SprintRetroThemes themes)
        => refs.Values
            .Where(theme => themes.IsRecurring(theme.Id))
            .Select(theme => new RetroMentionStripDto(theme, themes.Counts(theme.Id), themes.FixAt(theme.Id)))
            .OrderByDescending(strip => strip.Counts.Sum())
            .ThenBy(strip => strip.Theme.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static RetroActionRowDto ToActionRow(SprintActionRow action, DateOnly today, SprintRetroThemes themes, IReadOnlyDictionary<Guid, int> closedIndex)
    {
        var category = Enum.Parse<StateCategory>(action.StateCategory, ignoreCase: true);
        var planned = action.PlannedId is { } plannedId ? ToSprint(new SprintRow(plannedId, Guid.Empty, action.PlannedName!, action.PlannedStart!, action.PlannedEnd!), today) : null;
        var raised = action.RaisedId is { } raisedId ? ToSprint(new SprintRow(raisedId, Guid.Empty, action.RaisedName!, action.RaisedStart!, action.RaisedEnd!), today) : null;
        var owner = action.OwnerId is { } ownerId ? new UserRef(ownerId, action.OwnerName!, action.OwnerInitials!, action.OwnerColour!, action.OwnerPhotoUrl) : null;
        var theme = action.ThemeId is { } themeId ? new RetroThemeRefDto(themeId, action.ThemeName!, Enum.Parse<RetroTone>(action.ThemeTone!, ignoreCase: true)) : null;
        RetroVerdict? impact = category == StateCategory.Completed && action.ThemeId is { } id && closedIndex.TryGetValue(action.ItemId, out var closedAt)
            ? themes.Verdict(id, closedAt)
            : null;
        return new RetroActionRowDto(
            new RetroActionRefDto(action.ItemId, action.Key, action.Title, new StateRef(action.StateId, action.StateName, category), planned, action.Points),
            theme,
            raised,
            owner,
            impact);
    }

    private static SprintRef ToSprint(SprintRow row, DateOnly today)
        => new(row.Id, row.Name, SprintLabels.Phase(Day(row.Start), Day(row.End), today).ToStatus());

    private static DateOnly Day(string value) => DateOnly.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static TypeRef ToTypeRef(string type)
    {
        var id = Enum.Parse<WorkItemTypeId>(type, ignoreCase: true);
        return new TypeRef(id, WorkItemTypeRules.Name(id));
    }
}
