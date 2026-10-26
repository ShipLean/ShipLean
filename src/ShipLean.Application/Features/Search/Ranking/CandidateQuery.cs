using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Search.Ranking;

// ProjectIds are the ones the ranker has already checked access for; every candidate SQL still joins project_member for
// the current user. Vector is the embedded query: the ranker sets it only when the embedder is semantic, and a semantic
// leg or group given null returns Available = false (docs/search.md).
public sealed record CandidateQuery(
    string Text,
    IReadOnlyList<Guid> ProjectIds,
    IReadOnlyList<WorkItemTypeId>? Types,
    IReadOnlyList<string>? Tags,
    bool IncludeClosed,
    bool Prefix,
    CriteriaJson? Criteria,
    float[]? Vector);
