using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.WorkItems.Reads.Changes;

// One change to an item: a revision (Revision and Fields set, Comment null) or a comment (Comment set, Revision and Fields null). ActorId and
// ActorName are null when the import wrote the revision or the author is gone. Comment is the markdown body as written.
public sealed record ChangeEntryDto(
    Guid WorkItemId,
    string Key,
    string Title,
    ChangeKind Kind,
    Guid? ActorId,
    string? ActorName,
    DateTimeOffset At,
    IReadOnlyList<FieldChange>? Fields,
    string? Comment,
    int? Revision = null);
