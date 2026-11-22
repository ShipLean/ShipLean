using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// Reads back what record_retro_analysis stored.
[McpServerToolType]
internal sealed class GetRetroAnalysisTool(ProjectResolver projects, IProjectAccess access, CaptureSupport support, IRetroAnalysisReads reads)
{
    [McpServerTool(Name = "get_retro_analysis"), McpScope(McpScopes.Read)]
    [Description("Read an aggregate retro analysis. With `id`: its scope, funnel, ranked problem patterns with evidence and fixes, went-well patterns, fixes by tier with the action item each one rides on, the fixes that were discarded and the report. Without `id`: the project's newest 10 analyses. Nothing is written.")]
    public async Task<CallToolResult> GetRetroAnalysis(
        [Description("The analysis id. Leave out to list the project's newest analyses.")] string? id = null,
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: false, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        var openUrl = McpOpenUrls.Retros(await support.BaseUrlAsync(cancellationToken), resolved.Key);

        if (string.IsNullOrWhiteSpace(id))
        {
            var analyses = await reads.ListAsync(resolved.Id, cancellationToken);
            return PlanningReply.Of(new ListRetroAnalysesResult(resolved.Key, openUrl, analyses), $"{analyses.Count} analyses, newest first. Call get_retro_analysis with an id to read one.");
        }

        if (!Guid.TryParse(id.Trim(), out var analysisId))
        {
            throw ApiException.Validation("id is an analysis id.", "id");
        }

        var view = await reads.GetAsync(resolved.Id, analysisId, cancellationToken) ?? throw ApiException.NotFound("No such analysis in this project.");
        var result = new GetRetroAnalysisResult(
            resolved.Key, openUrl, view.Id, view.Scope, view.CreatedAt, view.AgentName, view.EmbeddingModel, view.RewriteModel, view.Funnel, view.Patterns, view.WentWell,
            view.FixesByTier, view.Discarded, view.ReportMarkdown);
        return PlanningReply.Of(result, $"Analysis {view.Id}: {view.Patterns.Count} problem patterns, {view.WentWell.Count} went-well patterns.");
    }
}
