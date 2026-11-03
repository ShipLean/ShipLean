using System.Globalization;
using System.Text.Json;
using Dapper;
using Npgsql;
using ShipLean.Application.Sprints;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Retros;
using ShipLean.Core;

namespace ShipLean.Application.Features.Retros.Search;

// Retrospectives > Search: one row per live point of a live retro, best match first (newest first without a query). The query
// matches a point's text and root cause, its theme's name, its retro's work item key and title, and the retro's raw failure log.
// Recurring is the project-wide rule of the sprint view and Insights (RetroRecurringThemes). A removed action item is already left off the point by RetroCards.
public sealed class RetroSearchReader(NpgsqlDataSource dataSource, TimeProvider clock, IRetroCards cards) : IRetroSearchReader
{
    public async Task<RetroSearchDto> SearchAsync(Guid projectId, RetroSearchQuery query, CancellationToken cancellationToken = default)
    {
        Check(query);
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var today = SprintLabels.Today(clock);
        var sprints = (await connection.QueryAsync<SprintRow>(new CommandDefinition(RetroSearchSql.Sprints, new { projectId }, cancellationToken: cancellationToken)))
            .Select(row => ToSprint(row, today))
            .ToList();
        var themes = (await connection.QueryAsync<ThemeRow>(new CommandDefinition(RetroSearchSql.Themes, new { projectId }, cancellationToken: cancellationToken)))
            .Select(row => new SearchTheme(row.Id, row.ThemeName, Enum.Parse<RetroTone>(row.Tone, ignoreCase: true)))
            .ToList();
        var recurring = await RetroRecurringThemes.LoadAsync(connection, projectId, sprints, themes, cancellationToken);

        var text = query.Q?.Trim() ?? string.Empty;
        var parameters = new
        {
            projectId,
            storyLevel = WorkItemTypeRules.StoryLevelDb,
            hasQuery = text.Length > 0,
            query = text,
            pattern = "%" + Escape(text) + "%",
            sprint = query.Sprint,
            category = query.Category is { } category ? Name(category) : null,
            theme = query.Theme,
            level = query.Level is { } level ? Name(level) : null,
            role = query.Role is { } role ? Name(role) : null,
            noSprint = query.NoSprint == true,
            unthemed = query.Unthemed == true,
            decision = query.Decision is { } decision ? Name(decision) : null,
            onlyThemes = query.Recurring == true ? recurring.ToArray() : null,
        };
        var found = (await connection.QueryAsync<FoundRow>(new CommandDefinition(RetroSearchSql.Rows, parameters, cancellationToken: cancellationToken))).ToList();

        var selectedId = query.Selected is { } selected
            ? await connection.QuerySingleOrDefaultAsync<Guid?>(new CommandDefinition(RetroSearchSql.LiveRetro, new { selected, projectId }, cancellationToken: cancellationToken))
            : null;
        var retroIds = found.Select(row => row.RetroId).Concat(selectedId is { } id ? [id] : []).Distinct().ToList();
        var loaded = await cards.LoadAsync(retroIds, cancellationToken);
        var points = loaded.SelectMany(card => card.Points).ToDictionary(point => point.Id);

        var rows = found.Where(row => points.ContainsKey(row.Id)).Select(row => Shape(points[row.Id], recurring)).ToList();
        var card = selectedId is { } cardId && loaded.FirstOrDefault(c => c.Id == cardId) is { } loadedCard
            ? loadedCard with { Points = [.. loadedCard.Points.Select(point => Shape(point, recurring))] }
            : null;
        var logOnly = found.Where(row => row.LogOnly && points.ContainsKey(row.Id)).Select(row => row.Id).ToList();
        return new RetroSearchDto(rows, logOnly, found.Count > 0 ? found[0].Total : 0, [.. sprints.Select(sprint => sprint.ToRef())], [.. themes.Select(theme => theme.ToRef())], card);
    }

    private static RetroPointDto Shape(RetroPointDto point, IReadOnlySet<Guid> recurring)
        => point with { Recurring = point.Theme is { } theme && recurring.Contains(theme.Id) };

    private static void Check(RetroSearchQuery query)
    {
        if (query.Category is { } category && !Enum.IsDefined(category))
        {
            throw ApiException.Validation("category is not a retro category.", "category");
        }
        if (query.Level is { } level && !Enum.IsDefined(level))
        {
            throw ApiException.Validation("level is stories or features.", "level");
        }
        if (query.Decision is { } decision && !Enum.IsDefined(decision))
        {
            throw ApiException.Validation("decision is undecided, actioned or notNeeded.", "decision");
        }
        if (query.Role is { } role && !Enum.IsDefined(role))
        {
            throw ApiException.Validation("role is conductor, subAgent or person.", "role");
        }
    }

    private static SearchSprint ToSprint(SprintRow row, DateOnly today)
    {
        var start = DateOnly.ParseExact(row.StartDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var end = DateOnly.ParseExact(row.EndDate, "yyyy-MM-dd", CultureInfo.InvariantCulture);
        return new SearchSprint(row.Id, row.SprintName, start, end, SprintLabels.Phase(start, end, today));
    }

    private static string Name<T>(T value)
        where T : struct, Enum
        => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static string Escape(string text) => text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private sealed record SprintRow(Guid Id, string SprintName, string StartDate, string EndDate);

    private sealed record ThemeRow(Guid Id, string ThemeName, string Tone);

    private sealed record FoundRow(Guid Id, Guid RetroId, Guid? ThemeId, bool LogOnly, int Total);
}
