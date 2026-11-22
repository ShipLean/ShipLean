using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Planning.Lessons;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// The first step of an aggregate retro analysis: a page of a scope's points, each with the context an agent needs to rewrite it as a generic lesson.
[McpServerToolType]
internal sealed class ExportRetroPointsTool(ItemResolver items, ProjectResolver projects, SprintResolver sprints, IProjectAccess access, CaptureSupport support, FeatureKind features, IRetroExport export)
{
    private const int DefaultLimit = 50;
    private const int MaxLimit = 100;

    [McpServerTool(Name = "export_retro_points"), McpScope(McpScopes.Read)]
    [Description("Export the retrospective points of one scope (a Feature, a sprint, or a date range) for an aggregate analysis, a page at a time. Each point comes with its root cause, item, Feature, sprint, agent and the item's commits; each retro on the page comes once with its failure log. Points already actioned or marked Not needed are left out, and so are points an earlier analysis recorded as noise, folded or oneOff unless exclude_covered is false. The first page also lists the project's open action items and, with with_patterns_from, the patterns of an earlier analysis. Give exactly one scope. Pages are 50 points by default (limit 1 to 100); pass next_cursor as cursor for the next page until it is null. Nothing is written. planning_guide with part retro_analysis holds the method that follows.")]
    public async Task<CallToolResult> ExportRetroPoints(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("Scope: a Feature's key. Its own retro plus its stories' and bugs'.")] string? feature = null,
        [Description("Scope: current, next or a sprint name.")] string? sprint = null,
        [Description("Scope: the first day of a date range (yyyy-MM-dd, UTC, inclusive, by the day a point was logged). Give with `to`.")] string? from = null,
        [Description("Scope: the last day of a date range (yyyy-MM-dd, UTC, inclusive). Give with `from`.")] string? to = null,
        [Description("false leaves went-well points out. Default true.")] bool? include_went_well = null,
        [Description("false brings back the points an earlier analysis recorded as noise, folded or oneOff. Default true.")] bool? exclude_covered = null,
        [Description("`latest`, or an analysis id: the first page also carries that analysis's patterns with their centroids.")] string? with_patterns_from = null,
        [Description("Points per page, 1 to 100. Default 50.")] int? limit = null,
        [Description("The next_cursor of the previous page. Leave out for the first page.")] string? cursor = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        var scope = await RetroScopeArgs.RequireOneAsync(items, sprints, features, resolved.Id, feature, sprint, from, to, cancellationToken);
        if (limit is < 1 or > MaxLimit)
        {
            throw ApiException.Validation($"limit is 1 to {MaxLimit}.", "limit");
        }

        var options = new RetroExportOptions(
            include_went_well ?? true, exclude_covered ?? true, string.Equals(with_patterns_from?.Trim(), "latest", StringComparison.OrdinalIgnoreCase), PatternsFrom(with_patterns_from),
            limit ?? DefaultLimit, string.IsNullOrWhiteSpace(cursor) ? null : RetroExportCursors.Decode(cursor));

        var page = await export.ExportAsync(resolved.Id, scope, options, cancellationToken);
        var result = new ExportRetroPointsResult(
            resolved.Key, McpOpenUrls.Retros(await support.BaseUrlAsync(cancellationToken), resolved.Key), page.Scope, page.TotalPoints,
            page.NextCursor is null ? null : RetroExportCursors.Encode(page.NextCursor), page.Points, page.Retros, page.OpenActions, page.PreviousPatterns);
        return PlanningReply.Of(result, $"{page.Points.Count} of {page.TotalPoints} points{(result.NextCursor is null ? ", the last page." : "; pass next_cursor as cursor for the next page.")}");
    }

    private static Guid? PatternsFrom(string? value)
    {
        if (string.IsNullOrWhiteSpace(value) || string.Equals(value.Trim(), "latest", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return Guid.TryParse(value.Trim(), out var id) ? id : throw ApiException.Validation("with_patterns_from is `latest` or an analysis id.", "with_patterns_from");
    }
}
