namespace ShipLean.Contracts.Search;

// Semantic is false when the words-only fallback ran (embeddings off, model failed, or some rows not embedded yet).
public sealed record SearchResults(
    bool Semantic,
    IReadOnlyList<SearchResultProject> Projects,
    IReadOnlyList<SearchResultSprint> Sprints,
    IReadOnlyList<SearchResultItem> Items,
    bool More);
