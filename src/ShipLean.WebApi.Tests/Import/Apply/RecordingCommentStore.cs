using ShipLean.Contracts.Comments;

namespace ShipLean.WebApi.Tests.Import.Apply;

// The comment store as the importer sees it: records what ImportAsync is given. Nothing else is called.
internal sealed class RecordingCommentStore : ICommentStore
{
    private readonly List<(Guid WorkItemId, ImportedComment Comment)> _imported = [];

    public IReadOnlyList<(Guid WorkItemId, ImportedComment Comment)> Imported
    {
        get
        {
            lock (_imported)
            {
                return [.. _imported];
            }
        }
    }

    public Task<Guid> ImportAsync(Guid workItemId, ImportedComment comment, CancellationToken cancellationToken = default)
    {
        lock (_imported)
        {
            _imported.Add((workItemId, comment));
        }

        return Task.FromResult(Guid.NewGuid());
    }

    public Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<CommentDto> AddAsync(Guid workItemId, CommentCreate create, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<CommentDto> EditAsync(Guid id, CommentEdit edit, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<CommentDto> SupersedeAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
