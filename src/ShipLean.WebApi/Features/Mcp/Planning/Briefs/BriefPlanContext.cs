using ShipLean.Application.Features.Mcp.Planning.Markers;
using ShipLean.Application.Features.Mcp.Planning.Tree;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Comments;
using ShipLean.Core;

namespace ShipLean.WebApi.Features.Mcp.Planning.Briefs;

// The rule and recent comments every plan brief starts with. Comment times stay UTC and say so, as agent prompts do
// (SHIP-1702 D5): a brief is read by an agent, not shown to a viewer.
public static class BriefPlanContext
{
    // The newest this many comments are shown; a cut drops the oldest, never the newest amendments.
    public const int MaxComments = 30;

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

        var sources = new List<(Guid Id, string Key)> { (tree.Feature.Id, tree.Feature.Key) };
        if (storyId is not null && tree.Stories.FirstOrDefault(story => story.Id == storyId) is { } owner)
        {
            sources.Add((owner.Id, owner.Key));
        }
        var all = new List<(string Key, CommentDto Comment)>();
        foreach (var (id, key) in sources)
        {
            foreach (var comment in (await comments.ListAsync(id, cancellationToken)).Where(c => c.CreatedAt >= since))
            {
                all.Add((key, comment));
            }
        }
        if (all.Count == 0)
        {
            return text + "\n\nComments since the plan was captured: none yet. Use `get_work_item` on the Feature or story for the full log.";
        }
        // Newest first so a cut drops the oldest; shown oldest to newest so the list still reads in order.
        var newest = all.OrderByDescending(entry => entry.Comment.CreatedAt).ToList();
        var shown = newest.Take(MaxComments).OrderBy(entry => entry.Comment.CreatedAt).ToList();
        var lines = shown.Select(entry =>
            $"- {entry.Key} ({DisplayDate.Format(entry.Comment.CreatedAt)}) {entry.Comment.Body.ReplaceLineEndings(" ")}");
        text += "\n\nComments since the plan was captured:\n" + string.Join("\n", lines);
        var omitted = newest.Skip(MaxComments).ToList();
        if (omitted.Count > 0)
        {
            var before = shown[0].Comment.CreatedAt.ToString("O");
            var where = string.Join(" and ", omitted.GroupBy(entry => entry.Key).Select(group => $"{group.Key} ({group.Count()})"));
            var keys = string.Join(" and ", omitted.Select(entry => entry.Key).Distinct().Select(key => $"`{key}`"));
            text += $"\n\n({omitted.Count} older {(omitted.Count == 1 ? "comment is" : "comments are")} not shown: {where}. "
                + $"Read {(omitted.Select(entry => entry.Key).Distinct().Count() == 1 ? "it" : "them")} with `get_work_item` on {keys} and `comments_before` = `{before}`, 5 at a time.)";
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
