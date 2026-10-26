using ShipLean.Contracts.Blobs;

namespace ShipLean.Contracts.Attachments;

// A work item's files; the bytes live in IBlobStore. Stores trust their caller: endpoints check IProjectAccess and the size limit first.
public interface IAttachmentStore
{
    // Attachments not archived, oldest first. Throws NotFound for an unknown work item.
    Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default);

    // Uploaded by ICurrentUser; source names the agent and tool when one attached it. A Markdown file up to 1 MB also stores its text for search.
    // Throws NotFound for an unknown work item, Validation for a blank name.
    Task<AttachmentDto> AddAsync(Guid workItemId, string name, string contentType, Stream content, AttachmentSource? source = null, CancellationToken cancellationToken = default);

    // The caller disposes the content. Throws NotFound when the attachment is archived or missing.
    Task<(AttachmentDto Attachment, BlobContent Content)> OpenAsync(Guid id, CancellationToken cancellationToken = default);

    // Hides the attachment; the row and its blob stay. Throws NotFound for an unknown or archived attachment.
    Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default);

    // Idempotent upsert keyed on work item + original system + id; returns the row id. Keeps the uploader and created time, replaces name, type and bytes on a re-run, and never changes the archived mark.
    // A re-run writes the bytes to a new blob and swaps the row to it only after that succeeds, so a failed stream keeps the old bytes. Throws NotFound for an unknown work item or uploader id.
    Task<Guid> ImportAsync(Guid workItemId, ImportedAttachment attachment, Stream content, CancellationToken cancellationToken = default);
}
