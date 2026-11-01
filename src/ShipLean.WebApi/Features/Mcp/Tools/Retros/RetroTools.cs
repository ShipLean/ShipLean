using System.ComponentModel;
using System.Globalization;
using System.Text.Json;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

[McpServerToolType]
internal sealed class RetroTools(ItemResolver items, ProjectResolver projects, SprintResolver sprints, IProjectAccess access, CaptureSupport support, IRetroLog log)
{
    [McpServerTool(Name = "log_retrospective"), McpScope(McpScopes.Write)]
    [Description(RetroDescriptions.LogRetrospective)]
    public async Task<CallToolResult> LogRetrospective(
        [Description("The key of the work item this retrospective is about, such as SHIP-12.")] string item,
        [Description("What went well.")] IReadOnlyList<RetroPointInput>? went_well = null,
        [Description("What went badly. Give each point a root_cause.")] IReadOnlyList<RetroPointInput>? went_badly = null,
        [Description("What can be improved.")] IReadOnlyList<RetroPointInput>? improve = null,
        [Description("What came up that the plan did not foresee.")] IReadOnlyList<RetroPointInput>? not_in_plan = null,
        [Description("Your raw failure log.")] RetroFailureLogInput? failure_log = null,
        [Description("The keys of the items you compiled this retrospective from, when you are the conductor.")] IReadOnlyList<string>? compiled_from = null,
        [Description("True when you log before the work is finished.")] bool? partial = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await items.ResolveAsync(item, ProjectRole.Member, cancellationToken: cancellationToken);
        var project = await support.ProjectOfAsync(resolved, cancellationToken);
        await access.RequireAsync(resolved.ProjectId, ProjectRole.Member, cancellationToken);

        var compiled = new List<Guid>();
        foreach (var key in compiled_from ?? [])
        {
            compiled.Add((await items.ResolveAsync(key, ProjectRole.Member, resolved.ProjectId, "compiled_from", cancellationToken)).Id);
        }

        var request = new RetroLogRequest(
            resolved.ProjectId, resolved.Id, Points(went_well), Points(went_badly), Points(improve), Points(not_in_plan),
            new RetroFailureLogDto(failure_log?.Blockers ?? [], failure_log?.AssumptionsDisproved ?? [], failure_log?.Friction ?? []),
            compiled, partial ?? false);
        var logged = await log.LogAsync(request, cancellationToken);

        var result = new LogRetrospectiveResult(
            project.Key, McpOpenUrls.WorkItem(await support.BaseUrlAsync(cancellationToken), project.Key, resolved.Key), logged.RetroId, logged.Item.Key,
            Name(logged.Role), logged.PointsAdded, Name(logged.ThemeMode), [.. logged.Themes.Select(Theme)], logged.Warnings);
        return PlanningReply.Of(result, $"Logged {logged.PointsAdded} retrospective points on {logged.Item.Key}.");
    }

    [McpServerTool(Name = "list_retrospectives"), McpScope(McpScopes.Read)]
    [Description(RetroDescriptions.ListRetrospectives)]
    public async Task<CallToolResult> ListRetrospectives(
        [Description("The project key, such as SHIP. Leave out for the most recent project.")] string? project = null,
        [Description("current, next, or a sprint name.")] string? sprint = null,
        [Description("A Feature's key: only points about that Feature.")] string? feature = null,
        [Description("went_well, went_badly, improve or not_in_plan.")] string? category = null,
        [Description("A theme name.")] string? theme = null,
        [Description("Only points logged on or after this date (yyyy-MM-dd).")] string? since = null,
        CancellationToken cancellationToken = default)
    {
        var resolvedProject = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        await access.RequireAsync(resolvedProject.Id, ProjectRole.Member, cancellationToken);
        var resolvedSprint = string.IsNullOrWhiteSpace(sprint) ? null : await sprints.ResolveAsync(resolvedProject.Id, sprint, cancellationToken: cancellationToken);
        var resolvedFeature = string.IsNullOrWhiteSpace(feature) ? null : await items.ResolveAsync(feature, ProjectRole.Member, resolvedProject.Id, "feature", cancellationToken);

        var listed = await log.ListAsync(
            new RetroListRequest(resolvedProject.Id, resolvedSprint?.Id, resolvedFeature?.Id, ParseCategory(category), string.IsNullOrWhiteSpace(theme) ? null : theme, ParseSince(since)),
            cancellationToken);

        var result = new ListRetrospectivesResult(
            resolvedProject.Key, McpOpenUrls.Sprints(await support.BaseUrlAsync(cancellationToken), resolvedProject.Key),
            [.. listed.Points.Select(Line)], Name(listed.ThemeMode), [.. listed.Themes.Select(Theme)]);
        return PlanningReply.Of(result, $"{listed.Points.Count} retrospective points in {resolvedProject.Key}.");
    }

    private static IReadOnlyList<RetroLogPoint> Points(IReadOnlyList<RetroPointInput>? points)
        => [.. (points ?? []).Select(p => new RetroLogPoint(p.Text ?? string.Empty, p.Theme, p.RootCause))];

    private static RetroThemeLine Theme(RetroThemeRefDto theme) => new(theme.Id, theme.Name, Name(theme.Tone));

    private static RetroPointLine Line(RetroPointDto point)
        => new(point.Id, point.RetroId, Name(point.Category), point.Text, point.RootCause, point.Theme?.Name, point.WorkItem?.Key, point.Sprint?.Name, Name(point.Author.Role), point.Author.AgentName);

    private static string Name<T>(T value) where T : struct, Enum => JsonNamingPolicy.CamelCase.ConvertName(value.ToString());

    private static RetroCategory? ParseCategory(string? text)
        => string.IsNullOrWhiteSpace(text)
            ? null
            : new string(text.Where(char.IsLetter).ToArray()).ToLowerInvariant() switch
            {
                "wentwell" => RetroCategory.WentWell,
                "wentbadly" => RetroCategory.WentBadly,
                "improve" => RetroCategory.Improve,
                "notinplan" => RetroCategory.NotInPlan,
                _ => throw ApiException.Validation($"\"{text}\" is not a category: use went_well, went_badly, improve or not_in_plan.", "category"),
            };

    private static DateOnly? ParseSince(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }
        if (!DateOnly.TryParseExact(text.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            throw ApiException.Validation($"\"{text}\" is not a date like 2026-10-31.", "since");
        }
        return date;
    }
}
