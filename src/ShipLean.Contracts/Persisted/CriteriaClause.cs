using System.Text.Json;

namespace ShipLean.Contracts.Persisted;

public sealed record CriteriaClause(string Field, CriteriaOp Op, JsonElement Value);
