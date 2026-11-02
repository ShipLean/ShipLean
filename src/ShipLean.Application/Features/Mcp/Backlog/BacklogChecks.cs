using ShipLean.Contracts.Backlog;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.WorkItems.Reads.Tree;

namespace ShipLean.Application.Features.Mcp.Backlog;

// IBacklogChecks over BacklogReviewer: what Features/Backlog reads of review_backlog.
public sealed class BacklogChecks(IWorkTreeReads reads, TimeProvider time) : IBacklogChecks
{
    private const int StaleDays = 30;

    public async Task<BacklogReviewDto> ReviewAsync(Guid projectId, CancellationToken cancellationToken = default)
    {
        var stack = await reads.StackAsync(projectId, StackLevel.Requirement, null, false, cancellationToken);
        var ids = stack.ToDictionary(row => row.Key, row => row.Id);
        var findings = BacklogReviewer.Review(stack, time.GetUtcNow(), StaleDays, BulkLimits.MaxRows);
        var checks = BacklogCheckIds.All
            .Select(id => new BacklogReviewCheckDto(id, [.. findings.Where(f => f.Flags.Contains(id)).Select(f => ids[f.Key])]))
            .ToList();
        return new BacklogReviewDto(BulkLimits.MaxRows, StaleDays, checks);
    }

    // Each title against the project's live stories and bugs, up to BacklogReviewer.CompareCap rows.
    public async Task<IReadOnlyList<DuplicateMatch>> DuplicatesAsync(Guid projectId, IReadOnlyList<string> titles, CancellationToken cancellationToken = default)
    {
        var stack = (await reads.StackAsync(projectId, StackLevel.Requirement, null, false, cancellationToken))
            .Where(row => WorkItemTypeRules.IsStoryOrBug(row.Type.Id)).Take(BacklogReviewer.CompareCap).ToList();
        return [.. titles
            .Select((title, index) => new DuplicateMatch(index, [.. stack.Where(row => BacklogReviewer.LooksAlike(title, row.Title)).Select(row => new DuplicateItem(row.Key, row.Title))]))
            .Where(match => match.Items.Count > 0)];
    }
}
