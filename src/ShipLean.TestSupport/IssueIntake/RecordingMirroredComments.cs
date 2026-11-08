using System.Collections.Concurrent;
using System.Data.Common;
using ShipLean.Contracts.IssueIntake;

namespace ShipLean.TestSupport.IssueIntake;

// An IMirroredComments that keeps each added comment in memory, keyed by its GitHub URL, and writes nothing to the database.
public sealed class RecordingMirroredComments : IMirroredComments
{
    private readonly ConcurrentDictionary<string, (Guid Id, MirroredComment Comment)> _added = new(StringComparer.Ordinal);

    public IReadOnlyList<MirroredComment> Added => [.. _added.Values.Select(v => v.Comment)];

    public Task<Guid?> FindByUrlAsync(string htmlUrl, DbTransaction? transaction = null, CancellationToken cancellationToken = default)
        => Task.FromResult(_added.TryGetValue(htmlUrl, out var found) ? found.Id : (Guid?)null);

    public Task<Guid> AddAsync(MirroredComment comment, DbTransaction transaction, CancellationToken cancellationToken = default)
    {
        var id = Guid.CreateVersion7();
        if (!_added.TryAdd(comment.HtmlUrl, (id, comment)))
        {
            throw new InvalidOperationException($"{comment.HtmlUrl} is already mirrored.");
        }
        return Task.FromResult(id);
    }
}
