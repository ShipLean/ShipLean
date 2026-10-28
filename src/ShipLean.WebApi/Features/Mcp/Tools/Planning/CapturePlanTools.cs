using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Plan;
using ShipLean.WebApi.Features.Mcp.Planning;
using ShipLean.WebApi.Features.Mcp.Planning.Capture;
using ShipLean.WebApi.Features.Mcp.Resolve;

namespace ShipLean.WebApi.Features.Mcp.Tools.Planning;

[McpServerToolType]
internal sealed class CapturePlanTools(
    ProjectResolver projects, IProjectAccess access, PlanEnvelope envelope, IAgentAddresses addresses,
    IPlanShapeRules rules, CapturePlanTargets targets, CapturePlanWriter writer, CapturePlanClaims claims)
{
    private const string PlanKind = "capture_plan";
    private const string ArgumentsChanged = "The arguments changed since the preview. Send the same arguments as the preview, or preview again.";

    [McpServerTool(Name = "capture_plan"), McpScope(McpScopes.Write, PreviewScope = McpScopes.Read)]
    [Description("Put a plan for several agents in the backlog: a feature (an existing one by key, or a new one), its stories (one per lane of a wave) and their tasks (one per agent brief), with the shared branch, the common brief and the plan document attached to the feature. A call without a plan comes back as a preview and a plan, with any warnings about the plan's shape; call again with the same arguments and that plan to create exactly what was previewed in one go; changed arguments are refused. Follow planning_guide when you write the plan.")]
    public async Task<CallToolResult> CapturePlan(
        [Description("The feature the plan lands under.")] CapturePlanFeature feature,
        [Description("The git branch every agent of the plan pushes to.")] string shared_branch,
        [Description("The brief every agent reads before its own, in Markdown.")] string common_brief,
        [Description("The stories, one per lane of a wave, each with its tasks.")] IReadOnlyList<CapturePlanStory> stories,
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("The whole plan document, in Markdown, attached to the feature.")] string? plan_markdown = null,
        [Description("The plan a preview returned. Send it with the same arguments as the preview to create exactly what was previewed; changed arguments are refused.")] string? plan = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        if (feature is null)
        {
            throw ApiException.Validation("Pass the feature: a key, or a title and goal.", "feature");
        }
        if (stories is null)
        {
            throw ApiException.Validation("Pass the stories: one per lane of a wave, each with its tasks.", "stories");
        }

        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        var openUrl = McpOpenUrls.Backlog(issuer, resolved.Key, "features");
        var input = new CapturePlanInput { Feature = feature, SharedBranch = shared_branch, CommonBrief = common_brief, PlanMarkdown = plan_markdown, Stories = stories };

        if (plan is not null)
        {
            var sealedPlan = envelope.Open<CapturePlanSeal>(PlanKind, plan, resolved.Id);
            if (sealedPlan.Digest != CapturePlanDigest.Of(input))
            {
                throw ApiException.Validation(ArgumentsChanged, "plan");
            }

            CapturePlanApplied done;
            if (await claims.FindAsync(sealedPlan.PlanId, resolved.Id, cancellationToken) is { } claimed)
            {
                // Already applied (a retry, or a response that was lost): no second tree, the same plan back.
                done = sealedPlan.FeatureId is { } sealedFeature && sealedFeature != claimed
                    ? throw ApiException.Validation(ArgumentsChanged, "plan")
                    : await writer.FinishAsync(claimed, input, cancellationToken);
            }
            else
            {
                var existing = await targets.ResolveAsync(feature, resolved.Id, cancellationToken);
                if (sealedPlan.FeatureId != existing?.Id)
                {
                    throw ApiException.Validation(ArgumentsChanged, "plan");
                }

                done = await writer.ApplyAsync(sealedPlan.PlanId, resolved.Id, input, existing, cancellationToken);
            }

            var applied = new CapturePlanResult(resolved.Key, openUrl, true, done.Feature, done.Stories, done.Attachments);
            return PlanningReply.Of(applied, $"Created the plan under {done.Feature}: {done.Stories.Count} stories and {done.Stories.Sum(s => s.Tasks.Count)} tasks.");
        }

        var target = await targets.ResolveAsync(feature, resolved.Id, cancellationToken);
        var findings = rules.Check(input);
        if (findings.Where(f => f.Severity == PlanFindingSeverity.Error).ToList() is { Count: > 0 } errors)
        {
            throw ApiException.Validation(string.Join(" ", errors.Select(e => $"{e.Field}: {e.Message}")), errors[0].Field);
        }

        var previewStories = CapturePlanMapper.Sort(input).Select(s => new PlanPreviewStory(s.Story.Title!, s.Wave.Text, s.Story.Lane, s.Story.Tasks?.Count ?? 0)).ToList();
        var preview = new CapturePlanPreview(
            resolved.Key, openUrl, false, target?.Key ?? feature.Title ?? "", previewStories,
            previewStories.Count + previewStories.Sum(s => s.Tasks) + (target is null ? 1 : 0),
            [.. findings.Where(f => f.Severity == PlanFindingSeverity.Warning).Select(f => new PlanWarning(f.Field, f.Message))],
            envelope.Seal(PlanKind, resolved.Id, new CapturePlanSeal(CapturePlanDigest.Of(input), target?.Id, Guid.NewGuid())), (int)PlanEnvelope.Lifetime.TotalMinutes);
        return PlanningReply.Of(preview, $"Preview of the plan for {resolved.Key}; nothing is created yet. Call capture_plan again with the same arguments and the `plan` value from the JSON below to create it.");
    }
}
