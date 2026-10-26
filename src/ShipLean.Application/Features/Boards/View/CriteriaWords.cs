using System.Text.Json;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Boards.View;

// A lane's criteria in words: "Priority = 1", "Assigned to = Priya Nair or Tags contains Urgent".
public static class CriteriaWords
{
    public static string Describe(CriteriaJson criteria, IReadOnlyDictionary<Guid, string> names)
        => string.Join(criteria.Match == CriteriaMatch.All ? " and " : " or ", criteria.Clauses.Select(c => Clause(c, names)));

    private static string Clause(CriteriaClause clause, IReadOnlyDictionary<Guid, string> names)
    {
        var label = FieldIds.All.First(f => f.Id == clause.Field).Label;
        var op = clause.Op switch
        {
            CriteriaOp.Equal => "=",
            CriteriaOp.NotEqual => "<>",
            CriteriaOp.GreaterThan => ">",
            CriteriaOp.LessThan => "<",
            CriteriaOp.In => "in",
            CriteriaOp.Contains => "contains",
            CriteriaOp.NotContains => "does not contain",
            CriteriaOp.IsEmpty => "is empty",
            _ => "is not empty",
        };
        return clause.Op is CriteriaOp.IsEmpty or CriteriaOp.IsNotEmpty ? $"{label} {op}" : $"{label} {op} {Value(clause.Value, names)}";
    }

    private static string Value(JsonElement value, IReadOnlyDictionary<Guid, string> names)
        => value.ValueKind switch
        {
            JsonValueKind.Array => "[" + string.Join(", ", value.EnumerateArray().Select(v => Value(v, names))) + "]",
            JsonValueKind.String when value.GetString() == "@me" => "me",
            JsonValueKind.String when Guid.TryParse(value.GetString(), out var id) && names.TryGetValue(id, out var name) => name,
            JsonValueKind.String => value.GetString()!,
            JsonValueKind.Null => "empty",
            _ => value.GetRawText(),
        };
}
