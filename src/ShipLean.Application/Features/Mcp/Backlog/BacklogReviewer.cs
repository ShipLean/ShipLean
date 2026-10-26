using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;
using ShipLean.Core;

namespace ShipLean.Application.Features.Mcp.Backlog;

// What review_backlog says about the top of a level's stack. Estimates are asked of stories and bugs, acceptance criteria
// of stories, a parent of everything below an epic. Likely duplicates compare a row's title with every row of the level
// (WordOverlap): they share at least two words and three quarters of the shorter title's words.
public static class BacklogReviewer
{
    // More than this many rows are not compared against each other.
    public const int CompareCap = 500;

    public static IReadOnlyList<BacklogFinding> Review(IReadOnlyList<StackRowDto> stack, DateTimeOffset now, int staleDays, int top)
    {
        var compared = stack.Take(CompareCap).Select(row => (Row: row, Own: Own(row.Title))).ToList();
        return [.. stack.Take(top).Select(row => Finding(row, compared, now, staleDays))];
    }

    private static BacklogFinding Finding(StackRowDto row, List<(StackRowDto Row, int Own)> compared, DateTimeOffset now, int staleDays)
    {
        var flags = new List<string>();
        var points = row.Type.Id is WorkItemTypeId.Story or WorkItemTypeId.Bug;
        if (points && row.Points is null)
        {
            flags.Add("unestimated");
        }
        if (row.Type.Id == WorkItemTypeId.Story && !row.HasAcceptanceCriteria)
        {
            flags.Add("no_acceptance_criteria");
        }
        if (row.Type.Id != WorkItemTypeId.Epic && row.Parent is null)
        {
            flags.Add("no_parent");
        }

        var days = Math.Max(0, (int)(now - row.ChangedAt).TotalDays);
        if (days >= staleDays)
        {
            flags.Add("stale");
        }

        var mine = Own(row.Title);
        var duplicates = compared.Where(other => other.Row.Id != row.Id && LooksAlike(row.Title, mine, other.Row.Title, other.Own)).Select(other => other.Row.Key).ToList();
        if (duplicates.Count > 0)
        {
            flags.Add("likely_duplicate");
        }

        return new BacklogFinding(row.Key, row.Title, row.Type.Name, row.State.Name, row.Points, days, flags, duplicates);
    }

    private static bool LooksAlike(string title, int own, string otherTitle, int otherOwn)
    {
        var shared = WordOverlap.Score(title, [otherTitle]);
        return shared >= 2 && shared * 4 >= Math.Min(own, otherOwn) * 3;
    }

    // How many distinct words a title has, as WordOverlap counts them.
    private static int Own(string title) => WordOverlap.Score(title, [title]);
}
