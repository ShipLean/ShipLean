using ShipLean.Contracts.Search;

namespace ShipLean.WebApi.Tests.Search.Index.Writer;

// An ISearchTextSource that returns one fixed text for its one id and records what the writer asked it for.
public sealed class RecordingTextSource(SearchSourceKind kind, SearchSourceText text) : ISearchTextSource
{
    private readonly List<Guid> _asked = [];
    private int _allIdsCalls;

    public SearchSourceKind Kind => kind;

    public int AllIdsCalls => Volatile.Read(ref _allIdsCalls);

    public IReadOnlyList<Guid> Asked
    {
        get
        {
            lock (_asked)
            {
                return [.. _asked];
            }
        }
    }

    public Task<IReadOnlyList<SearchSourceText>> TextsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default)
    {
        lock (_asked)
        {
            _asked.AddRange(ids);
        }
        return Task.FromResult<IReadOnlyList<SearchSourceText>>(ids.Contains(text.SourceId) ? [text] : []);
    }

    public Task<IReadOnlyList<Guid>> AllIdsAsync(CancellationToken cancellationToken = default)
    {
        Interlocked.Increment(ref _allIdsCalls);
        return Task.FromResult<IReadOnlyList<Guid>>([text.SourceId]);
    }
}
