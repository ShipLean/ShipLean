using ShipLean.Contracts.Persisted;
using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Search;

// ProjectIds null means every project the caller is in. Criteria (state, assignee, sprint, parent) needs exactly one
// project id; the ranker throws validation otherwise. Types and Tags work across projects.
public sealed record SearchQuery(
    string Text,
    IReadOnlyList<Guid>? ProjectIds = null,
    IReadOnlyList<WorkItemTypeId>? Types = null,
    IReadOnlyList<string>? Tags = null,
    bool IncludeClosed = false,
    bool Prefix = false,
    int Limit = 10,
    CriteriaJson? Criteria = null);
