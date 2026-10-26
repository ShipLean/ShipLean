using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Criteria;

// Criteria is null when there is none. Errors is empty when every clause is valid.
public sealed record CriteriaDto(CriteriaJson? Criteria, IReadOnlyList<CriteriaValidationError> Errors);
