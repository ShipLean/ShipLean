namespace ShipLean.Contracts.Comments;

// A work item's discussion. Stores trust their caller: endpoints check IProjectAccess first. The author is ICurrentUser.
public interface ICommentStore
{
    // Comments not archived, oldest first. Throws NotFound for an unknown work item.
    Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default);

    // Throws NotFound for an unknown work item, Validation for a blank or too long body.
    Task<CommentDto> AddAsync(Guid workItemId, CommentCreate create, CancellationToken cancellationToken = default);

    // Author only. Throws NotFound for an unknown or archived comment, Forbidden for anyone else, Validation for a blank or too long body.
    Task<CommentDto> EditAsync(Guid id, CommentEdit edit, CancellationToken cancellationToken = default);

    // Hides the comment; the row stays. Whether the caller may is the endpoint's rule (author or project admin). Throws NotFound for an unknown or archived comment.
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    // Marks the comment superseded by the caller and keeps it; already superseded keeps the first mark. Any member may (the endpoint checks access). Throws NotFound for an unknown or archived comment.
    Task<CommentDto> SupersedeAsync(Guid id, CancellationToken cancellationToken = default);

    // Idempotent upsert keyed on work item + original system + id; returns the row id. Keeps the author and created time, updates the body on a re-run, and never changes the archived mark. An imported comment is never "edited"
    // (EditedAt stays null): the real store writes updated_at = created_at on import so "edited" isn't derived from updated_at > created_at.
    // The importer attributes authors that match no user to the seeded Jira import user (SeedIds.Users.JiraImport). Throws NotFound for an unknown work item or author id, Validation for a bad body.
    Task<Guid> ImportAsync(Guid workItemId, ImportedComment comment, CancellationToken cancellationToken = default);
}
