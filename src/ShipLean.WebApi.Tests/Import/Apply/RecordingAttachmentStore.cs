using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Blobs;

namespace ShipLean.WebApi.Tests.Import.Apply;

// The attachment store as the importer sees it: records what ImportAsync is given, with the bytes it read. Nothing else is called.
internal sealed class RecordingAttachmentStore : IAttachmentStore
{
    private readonly List<(Guid WorkItemId, ImportedAttachment Attachment, byte[] Bytes)> _imported = [];

    public IReadOnlyList<(Guid WorkItemId, ImportedAttachment Attachment, byte[] Bytes)> Imported
    {
        get
        {
            lock (_imported)
            {
                return [.. _imported];
            }
        }
    }

    public async Task<Guid> ImportAsync(Guid workItemId, ImportedAttachment attachment, Stream content, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        lock (_imported)
        {
            _imported.Add((workItemId, attachment, buffer.ToArray()));
        }

        return Guid.NewGuid();
    }

    public Task<IReadOnlyList<AttachmentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<AttachmentDto> AddAsync(Guid workItemId, string name, string contentType, Stream content, AttachmentSource? source = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<(AttachmentDto Attachment, BlobContent Content)> OpenAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
