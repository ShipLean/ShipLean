using System.Text.Json;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Criteria;

// Builds a clause from a string or a list of strings, the shapes the MCP search filters need.
public static class CriteriaClauses
{
    public static CriteriaClause Of(string field, CriteriaOp op, string value) => new(field, op, JsonSerializer.SerializeToElement(value));

    public static CriteriaClause Of(string field, CriteriaOp op, string[] values) => new(field, op, JsonSerializer.SerializeToElement(values));
}
