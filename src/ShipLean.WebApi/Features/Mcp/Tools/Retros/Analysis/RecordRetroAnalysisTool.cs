using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Application.Features.Mcp.Planning.Lessons;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros;
using ShipLean.Contracts.Retros.Analysis;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Payloads;
using ShipLean.WebApi.Features.Mcp.Plan;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros.Analysis;

// Writes back an aggregate retro analysis. Without `plan` it previews and returns a plan that seals a digest of the arguments, as capture_plan does; with the plan and the same
// arguments (or the same payload) it applies. Applying needs shiplean:plan; a preview needs only shiplean:read.
[McpServerToolType]
internal sealed class RecordRetroAnalysisTool(
    ItemResolver items, ProjectResolver projects, SprintResolver sprints, IProjectAccess access, CaptureSupport support, FeatureKind features, PlanEnvelope envelope,
    IRetroAnalysisWriter writer, ICurrentUser user, ICurrentAgent agent)
{
    private const string PlanKind = "record_retro_analysis";
    private const string ArgumentsChanged = "The arguments changed since the preview. Send the same arguments as the preview, or preview again.";

    [McpServerTool(Name = "record_retro_analysis"), McpScope(McpScopes.Plan, PreviewScope = McpScopes.Read), McpPayload]
    [Description("Record an aggregate retro analysis: the points you looked at and what you decided about each, the patterns you clustered them into, the fixes you proposed for each problem pattern with how much of each is already enforced, and your report. Give the same scope you exported. ShipLean derives the funnel and each pattern's evidence from its own data, ranks the patterns, and makes one action item per problem pattern from its top-ranked fix (fix, structure, analyzer, test, then skill or guide); the pattern's other fixes are stored against the same action. A fix that is fully enforced or discarded makes no action. Without `plan` it only previews and returns a plan; call again with the same arguments, or the same payload, and that plan to apply exactly it; changed arguments are refused. When the arguments are large, upload them with create_payload_upload and pass payload. planning_guide with part retro_analysis holds the method.")]
    public async Task<CallToolResult> RecordRetroAnalysis(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("Scope: a Feature's key. Its own retro plus its stories' and bugs'.")] string? feature = null,
        [Description("Scope: current, next or a sprint name.")] string? sprint = null,
        [Description("Scope: the first day of a date range (yyyy-MM-dd, UTC, inclusive). Give with `to`.")] string? from = null,
        [Description("Scope: the last day of a date range (yyyy-MM-dd, UTC, inclusive). Give with `from`.")] string? to = null,
        [Description("The covered rule you exported with, so the funnel counts the same points. Default true.")] bool? exclude_covered = null,
        [Description("The name of the embedding model you clustered with. A later run with the same model can continue these patterns.")] string? embedding_model = null,
        [Description("The name of the model that rewrote the points, if you want it recorded.")] string? rewrite_model = null,
        [Description("Every point you looked at, each with its outcome.")] IReadOnlyList<RetroAnalysisPointInput>? points = null,
        [Description("The patterns you found, problem or wentWell. Every clustered point is in exactly one.")] IReadOnlyList<RetroAnalysisPatternInput>? patterns = null,
        [Description("The fixes you propose, each for a problem pattern, with how much of it is already enforced.")] IReadOnlyList<RetroAnalysisFixInput>? fixes = null,
        [Description("The summary report, in Markdown, at most 200 KB.")] string? report_markdown = null,
        [Description("The plan a preview returned. Send it with the same arguments as the preview to apply exactly what was previewed; changed arguments are refused.")] string? plan = null,
        [Description(McpPayloadTargets.ArgumentDescription)] string? payload = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        var openUrl = McpOpenUrls.Retros(await support.BaseUrlAsync(cancellationToken), resolved.Key);

        var input = new RecordRetroAnalysisInput
        {
            Feature = feature, Sprint = sprint, From = from, To = to, ExcludeCovered = exclude_covered, EmbeddingModel = embedding_model, RewriteModel = rewrite_model,
            Points = points, Patterns = patterns, Fixes = fixes, ReportMarkdown = report_markdown,
        };
        var sealedPlan = plan is null ? null : envelope.Open<RetroAnalysisSeal>(PlanKind, plan, resolved.Id);
        var scope = await RetroScopeArgs.RequireOneAsync(items, sprints, features, resolved.Id, feature, sprint, from, to, cancellationToken);
        var existing = new List<Guid?>();
        for (var index = 0; index < (fixes?.Count ?? 0); index++)
        {
            var key = fixes![index]?.Existing;
            existing.Add(string.IsNullOrWhiteSpace(key) ? null : (await items.ResolveAsync(key, ProjectRole.Member, resolved.Id, $"fixes[{index}].existing", cancellationToken)).Id);
        }

        var request = RecordRetroAnalysisParser.Parse(input, scope, existing);
        var digest = RetroAnalysisDigest.Of(input);

        if (sealedPlan is not null)
        {
            if (sealedPlan.Digest != digest)
            {
                throw ApiException.Validation(ArgumentsChanged, "plan");
            }

            var applied = await writer.ApplyAsync(resolved.Id, request, new RetroActor(user.Id, AgentStamp.Of(agent)), cancellationToken);
            return PlanningReply.Of(
                Result(resolved.Key, openUrl, true, applied, null, null),
                $"Applied: analysis {applied.AnalysisId}, {applied.Funnel.ActionsNew} action items created, {applied.Funnel.ActionsAdded} added to, {applied.Skipped.Count} points skipped.");
        }

        var outcome = await writer.PreviewAsync(resolved.Id, request, cancellationToken);
        var preview = Result(resolved.Key, openUrl, false, outcome, envelope.Seal(PlanKind, resolved.Id, new RetroAnalysisSeal(digest)), (int)PlanEnvelope.Lifetime.TotalMinutes);
        return PlanningReply.Of(
            preview,
            $"Preview: {outcome.Patterns.Count} patterns, {outcome.WouldCreate.Count} action items would be created, {outcome.WouldAdd.Count} added to, {outcome.Warnings.Count} warnings. Nothing was written; call record_retro_analysis again with the same arguments and the `plan` value from the JSON below to apply it.");
    }

    private static RecordRetroAnalysisResult Result(string project, string openUrl, bool applied, RetroAnalysisOutcome outcome, string? plan, int? expiresInMinutes)
        => new(
            project, openUrl, applied, outcome.Funnel, outcome.Patterns,
            [.. outcome.WouldCreate.Select(line => new DecideWouldCreateLine(line.Title, line.FixType.ToString().ToLowerInvariant(), line.Theme, line.Covers, [.. line.PossibleDuplicates.Select(open => new DecideDuplicateLine(open.Key, open.Title, open.State))]))],
            [.. outcome.WouldAdd.Select(line => new DecideWouldAddLine(line.Existing, line.Covers))],
            [.. outcome.Skipped.Select(skip => new DecideSkippedLine(skip.Point, skip.Reason))],
            outcome.Warnings, outcome.AnalysisId, outcome.Created, plan, expiresInMinutes);
}
