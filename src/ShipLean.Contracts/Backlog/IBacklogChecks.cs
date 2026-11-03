namespace ShipLean.Contracts.Backlog;

// The review_backlog checks, reached through Contracts because Features/Backlog may not reference Features/Mcp. Callers check access first.
public interface IBacklogChecks
{
    // The five checks over the top BulkLimits.MaxRows rows of the project's stories and bugs.
    Task<BacklogReviewDto> ReviewAsync(Guid projectId, CancellationToken cancellationToken = default);

    // The same test review_backlog uses for likely duplicates, for each title against the project's live stories and bugs.
    // Only titles with a match are returned.
    Task<IReadOnlyList<DuplicateMatch>> DuplicatesAsync(Guid projectId, IReadOnlyList<string> titles, CancellationToken cancellationToken = default);
}
