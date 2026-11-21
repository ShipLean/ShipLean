using System.ComponentModel;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Mcp;
using ShipLean.Contracts.Retros;
using ShipLean.WebApi.Features.Mcp.Contracts;
using ShipLean.WebApi.Features.Mcp.Plan;
using ShipLean.WebApi.Features.Mcp.Resolve;
using ShipLean.WebApi.Features.Mcp.Tools.Capture;
using ShipLean.WebApi.Features.Mcp.Tools.Planning;

namespace ShipLean.WebApi.Features.Mcp.Tools.Retros;

// Lists the theme merges agents proposed, and accepts or rejects them on a person's request: the same answer the web retros page gives, by the same
// IRetroMergeProposals. Without `plan` it only previews and returns a plan; with the plan it applies exactly what the plan holds.
[McpServerToolType]
internal sealed class DecideThemeMergesTools(ProjectResolver projects, IProjectAccess access, CaptureSupport support, PlanEnvelope envelope, IRetroMergeProposals proposals, ICurrentUser user, ICurrentAgent agent)
{
    private const string PlanKind = "decide_theme_merges";

    [McpServerTool(Name = "decide_theme_merges"), McpScope(McpScopes.Plan, PreviewScope = McpScopes.Read)]
    [Description("List the retrospective theme merges agents proposed (decide_retro_actions `merges`), and accept or reject them when the person you work for asks you to. Call it with no ids to list the pending proposals: id, the theme to fold away and the theme to keep, the reason, who proposed it and how many points sit on the folded theme. Give proposal ids in `merge` to accept them (every point on the folded theme, including ones a person themed, moves to the kept theme and the folded theme is removed) or in `keep_apart` to reject them. Without `plan` it only previews and returns a plan; call again with that plan to apply exactly it. Only run this when the person asked for these merges: an agent never merges themes on its own initiative. A proposal already answered is a conflict.")]
    public async Task<CallToolResult> DecideThemeMerges(
        [Description("The project key, such as SHIP. Needed when the user is in more than one project.")] string? project = null,
        [Description("Proposal ids to accept: fold the theme into the one to keep.")] IReadOnlyList<Guid>? merge = null,
        [Description("Proposal ids to reject: keep the two themes apart.")] IReadOnlyList<Guid>? keep_apart = null,
        [Description("The plan a preview returned. Applies exactly what it holds and ignores every other argument.")] string? plan = null,
        CancellationToken cancellationToken = default)
    {
        var resolved = await projects.ResolveAsync(project, forWrite: true, cancellationToken);
        await access.RequireAsync(resolved.Id, ProjectRole.Member, cancellationToken);
        var openUrl = McpOpenUrls.Retros(await support.BaseUrlAsync(cancellationToken), resolved.Key);

        if (plan is not null)
        {
            var body = envelope.Open<ThemeMergesPlan>(PlanKind, plan, resolved.Id);
            var before = await proposals.PendingAsync(resolved.Id, cancellationToken);
            var (accepted, rejected) = Pick(before, body.Merge, body.KeepApart);
            var actor = new RetroActor(user.Id, AgentStamp.Of(agent));
            foreach (var line in accepted)
            {
                await proposals.MergeAsync(line.Id, actor, cancellationToken);
            }

            foreach (var line in rejected)
            {
                await proposals.KeepApartAsync(line.Id, actor, cancellationToken);
            }

            var done = new DecideThemeMergesResult(resolved.Key, openUrl, true, accepted, rejected, Lines(await proposals.PendingAsync(resolved.Id, cancellationToken)), null, null);
            return PlanningReply.Of(done, $"Applied: {accepted.Count} merged, {rejected.Count} kept apart, {done.Pending.Count} proposals still pending.");
        }

        var pending = await proposals.PendingAsync(resolved.Id, cancellationToken);
        var mergeIds = Distinct(merge);
        var keepIds = Distinct(keep_apart);
        if (mergeIds.Count + keepIds.Count == 0)
        {
            var listed = new DecideThemeMergesResult(resolved.Key, openUrl, false, [], [], Lines(pending), null, null);
            return PlanningReply.Of(listed, $"{pending.Count} theme merge proposals pending. Nothing was changed; call decide_theme_merges with proposal ids in `merge` or `keep_apart` to preview an answer.");
        }

        if (mergeIds.Intersect(keepIds).Any())
        {
            throw ApiException.Validation("A proposal is either accepted or rejected, not both.", "keep_apart");
        }

        var (willMerge, willKeep) = Pick(pending, mergeIds, keepIds);
        var preview = new DecideThemeMergesResult(
            resolved.Key, openUrl, false, willMerge, willKeep, Lines(pending),
            envelope.Seal(PlanKind, resolved.Id, new ThemeMergesPlan(mergeIds, keepIds)), (int)PlanEnvelope.Lifetime.TotalMinutes);
        return PlanningReply.Of(preview, $"Preview: {willMerge.Count} proposals would be merged, {willKeep.Count} kept apart. Nothing was written; call decide_theme_merges again with the `plan` value from the JSON below to apply it.");
    }

    private static List<Guid> Distinct(IReadOnlyList<Guid>? ids) => [.. (ids ?? []).Distinct()];

    // Every named proposal must still be waiting: an answered one (or one of another project) is a conflict, as on the web.
    private static (IReadOnlyList<ThemeMergeProposalLine> Merge, IReadOnlyList<ThemeMergeProposalLine> KeepApart) Pick(
        IReadOnlyList<RetroMergeProposalDto> pending, IReadOnlyList<Guid> merge, IReadOnlyList<Guid> keepApart)
    {
        var byId = pending.ToDictionary(proposal => proposal.Id);
        ThemeMergeProposalLine[] Find(IReadOnlyList<Guid> ids, string field)
            => [.. ids.Select(id => byId.TryGetValue(id, out var found)
                ? Line(found)
                : throw ApiException.Conflict($"Proposal {id} is not waiting for an answer: it was already answered, or it is not in this project ({field})."))];
        return (Find(merge, "merge"), Find(keepApart, "keep_apart"));
    }

    private static IReadOnlyList<ThemeMergeProposalLine> Lines(IReadOnlyList<RetroMergeProposalDto> pending) => [.. pending.Select(Line)];

    private static ThemeMergeProposalLine Line(RetroMergeProposalDto proposal)
        => new(proposal.Id, proposal.From.Name, proposal.Into.Name, proposal.Reason, proposal.ProposedBy.AgentName ?? proposal.ProposedBy.User.Name, proposal.PointCount);

    private sealed record ThemeMergesPlan(IReadOnlyList<Guid> Merge, IReadOnlyList<Guid> KeepApart);
}
