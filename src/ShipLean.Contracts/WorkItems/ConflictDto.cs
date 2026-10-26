namespace ShipLean.Contracts.WorkItems;

// The 409 body for a stale expectedRevision: a superset of ApiError (Error, Code = "conflict") plus the item as it is now
// and the revisions written since the caller's, newest first.
public sealed record ConflictDto(string Error, string Code, WorkItemDto Current, IReadOnlyList<RevisionDto> TheirChanges);
