using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Comments;

namespace ShipLean.WebApi.Features.Mcp.Planning.Briefs;

// The rule and recent comments every plan brief starts with.
public static class BriefPlanContext
{
    public const string StateRule =
        "Progress, reports, decisions and brief changes live in ShipLean on the plan's items — use `comment`, `report_task`, and `get_work_item`; do not keep them in local files.";

    public static async Task<string> PrefixAsync(
        IAttachmentStore attachments,
        ICommentStore comments,
        PlanTree tree,
        Guid? storyId,
        CancellationToken cancellationToken)
    {
        var text = StateRule;
        var since = await PlanCapturedAtAsync(attachments, tree.Feature.Id, cancellationToken);
        if (since is null)
        {
            return text;
        }

        var ids = new List<Guid> { tree.Feature.Id };
        if (storyId is not null)
        {
            ids.Add(storyId.Value);
        }
        var lines = new List<string>();
        foreach (var id in ids)
        {
            foreach (var comment in (await comments.ListAsync(id, cancellationToken)).Where(c => c.CreatedAt >= since))
            {
                lines.Add($"- ({comment.CreatedAt:u}) {comment.Body.ReplaceLineEndings(" ")}");
            }
        }
        if (lines.Count == 0)
        {
            text += "\n\nComments since the plan was captured: none yet. Use `get_work_item` on the Feature or story for the full log.";
            return text;
        }
        text += "\n\nComments since the plan was captured:\n" + string.Join("\n", lines.Take(30));
        if (lines.Count > 30)
        {
            text += "\n\n(More comments exist; page with `get_work_item` and `comments_before`.)";
        }
        return text;
    }

    private static async Task<DateTimeOffset?> PlanCapturedAtAsync(IAttachmentStore attachments, Guid featureId, CancellationToken cancellationToken)
    {
        var plan = (await attachments.ListAsync(featureId, cancellationToken))
            .LastOrDefault(file => string.Equals(file.Name, PlanFiles.Plan, StringComparison.Ordinal));
        return plan?.CreatedAt;
    }
}
