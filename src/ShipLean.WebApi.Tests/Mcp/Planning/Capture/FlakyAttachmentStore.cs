using System.Data.Common;
using ShipLean.Contracts.Attachments;

namespace ShipLean.WebApi.Tests.Mcp.Planning.Capture;

// A test double that wraps the real attachment store and fails the next AddAsync calls a test armed, as a blob or database
// error would; everything else, and every call after the armed ones, goes straight to the real store.
public sealed class FlakyAttachmentStore(IAttachmentStore inner, AttachFailures failures) : IAttachmentStore
{
    public Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => inner.ListAsync(workItemId, cancellationToken);

    public Task<AttachmentDto> AddAsync(Guid workItemId, string name, string contentType, Stream content, AttachmentSource? source = null, CancellationToken cancellationToken = default)
        => failures.TryFail() ? throw new IOException("The blob store is down.") : inner.AddAsync(workItemId, name, contentType, content, source, cancellationToken);

    public Task<(AttachmentDto Attachment, ShipLean.Contracts.Blobs.BlobContent Content)> OpenAsync(Guid id, CancellationToken cancellationToken = default)
        => inner.OpenAsync(id, cancellationToken);

    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => inner.ArchiveAsync(id, cancellationToken);

    public Task<Guid> ImportAsync(Guid workItemId, ImportedAttachment attachment, Stream content, CancellationToken cancellationToken = default)
        => inner.ImportAsync(workItemId, attachment, content, cancellationToken);

    public Task<StagedAttachment> StageAsync(Guid workItemId, string name, string contentType, Stream content, AttachmentSource? source = null, long? maxBytes = null, CancellationToken cancellationToken = default)
        => inner.StageAsync(workItemId, name, contentType, content, source, maxBytes, cancellationToken);

    public Task<IReadOnlyList<AttachmentDto>> CommitStagedAsync(Guid workItemId, IReadOnlyList<StagedAttachment> staged, Func<DbTransaction, CancellationToken, Task>? inTransaction = null, CancellationToken cancellationToken = default)
        => inner.CommitStagedAsync(workItemId, staged, inTransaction, cancellationToken);
}
