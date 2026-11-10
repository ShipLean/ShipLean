namespace ShipLean.Application.Features.Inbox;

public sealed class AskInboxSource : IInboxAskSource
{
    // TODO: read open decisions and questions on items the user owns; until then there are none.
    public Task<IReadOnlyList<InboxAskRow>> OpenAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<InboxAskRow>>([]);

    public Task<IReadOnlyDictionary<Guid, int>> OpenCountsByProjectAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyDictionary<Guid, int>>(new Dictionary<Guid, int>());

    public Task<bool> EverAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(false);
}
