using ShipLean.Contracts.Comments;
using ShipLean.Contracts.Refs;

namespace ShipLean.WebApi.Tests.Mcp.Orient;

// A comment store that knows every work item, pinned over the registered in-memory fake: that fake only lists comments
// for items arranged on it with AddWorkItem, which a database-arranged item never is. Only list and add are used here.
internal sealed class ListedCommentStore : ICommentStore
{
    private readonly List<CommentDto> _comments = [];

    public Task<IReadOnlyList<CommentDto>> ListAsync(Guid workItemId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<CommentDto>>([.. _comments.Where(c => c.WorkItemId == workItemId)]);

    public Task<CommentDto> AddAsync(Guid workItemId, CommentCreate create, CancellationToken cancellationToken = default)
    {
        var created = new DateTimeOffset(2026, 1, 1, 9, 0, 0, TimeSpan.Zero).AddMinutes(_comments.Count);
        var comment = new CommentDto(Guid.NewGuid(), workItemId, new UserRef(Guid.NewGuid(), "Joel Dickson", "JD", "#2f7fd8", null), create.Body, create.Body, created, "arranged", null, null);
        _comments.Add(comment);
        return Task.FromResult(comment);
    }

    public Task<CommentDto> EditAsync(Guid id, CommentEdit edit, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<CommentDto> SupersedeAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task ArchiveAsync(Guid id, CancellationToken cancellationToken = default) => throw new NotSupportedException();

    public Task<Guid> ImportAsync(Guid workItemId, ImportedComment comment, CancellationToken cancellationToken = default) => throw new NotSupportedException();
}
