namespace ShipLean.Contracts.Criteria;

// ClauseIndex is null for an error on the document itself (no clauses).
public sealed record CriteriaValidationError(int? ClauseIndex, string Field, string Code, string Message);
