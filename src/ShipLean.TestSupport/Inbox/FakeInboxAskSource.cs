using ShipLean.Application.Features.Inbox;

namespace ShipLean.TestSupport.Inbox;

// An IInboxAskSource that returns exactly what it was given, whatever the user or project asked for: for tests of the reader
// that composes the sources. Register it in place of the real sources.
public sealed class FakeInboxAskSource(
    IReadOnlyList<InboxAskRow>? rows = null, IReadOnlyDictionary<Guid, int>? counts = null, bool ever = false) : IInboxAskSource
{
    public Task<IReadOnlyList<InboxAskRow>> OpenAsync(Guid userId, Guid projectId, CancellationToken cancellationToken = default)
        => Task.FromResult(rows ?? []);

    public Task<IReadOnlyDictionary<Guid, int>> OpenCountsByProjectAsync(Guid userId, CancellationToken cancellationToken = default)
        => Task.FromResult(counts ?? new Dictionary<Guid, int>());

    public Task<bool> EverAsync(Guid userId, CancellationToken cancellationToken = default) => Task.FromResult(ever);
}
