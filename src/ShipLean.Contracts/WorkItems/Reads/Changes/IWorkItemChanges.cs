namespace ShipLean.Contracts.WorkItems.Reads.Changes;

// What changed in a project after a cursor: revisions and comments of its items. Reorders write no revision, so they
// never appear. Callers check project access first. Scoped to the project id: another project's items give nothing.
// Comment edits are not changes: a comment's entry time is its creation. The cursor orders by write time, so a
// transaction that commits after a reader has paged past its timestamp is not returned; a caller that needs
// completeness re-reads a small overlap window.
public interface IWorkItemChanges
{
    // The cursor is opaque (a timestamp plus a tiebreak, so equal times page cleanly); null = from the start. Limit is
    // 1..500. mineUserId, when given, keeps only items assigned to or created by that user. Archived comments are left out.
    // Throws ApiException.Validation (the cursor is not one this read gave, or the limit is out of range).
    Task<ChangesPageDto> SinceAsync(Guid projectId, string? cursor, int limit, Guid? mineUserId, CancellationToken cancellationToken = default);

    // The same stream from a point in time instead of a cursor: every entry at or after `since`. The page carries a cursor
    // as SinceAsync does (for an empty page, one that resumes from `since`). Limit and mineUserId as above.
    // Throws ApiException.Validation (the limit is out of range).
    Task<ChangesPageDto> SinceTimeAsync(Guid projectId, DateTimeOffset since, int limit, Guid? mineUserId, CancellationToken cancellationToken = default);
}
