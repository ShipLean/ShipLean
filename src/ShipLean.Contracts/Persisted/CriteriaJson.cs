namespace ShipLean.Contracts.Persisted;

public sealed record CriteriaJson(CriteriaMatch Match, IReadOnlyList<CriteriaClause> Clauses);
