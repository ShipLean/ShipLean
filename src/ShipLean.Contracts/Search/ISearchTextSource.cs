namespace ShipLean.Contracts.Search;

// One per source kind. TextsAsync leaves out the ids that no longer exist or are archived, removed or without text,
// so the writer drops their rows.
[MultipleImplementations]
public interface ISearchTextSource
{
    SearchSourceKind Kind { get; }

    Task<IReadOnlyList<SearchSourceText>> TextsAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> AllIdsAsync(CancellationToken cancellationToken = default);
}
