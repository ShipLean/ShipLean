namespace ShipLean.Contracts.Views.Search;

// GET /api/search. Query echoes the text searched. Semantic is false when the words-only fallback ran.
public sealed record SearchViewDto(
    string Query,
    bool Semantic,
    IReadOnlyList<SearchProjectDto> Projects,
    IReadOnlyList<SearchSprintDto> Sprints,
    IReadOnlyList<SearchItemDto> Items,
    bool More);
