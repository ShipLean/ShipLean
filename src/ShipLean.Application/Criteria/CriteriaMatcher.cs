using System.Globalization;
using System.Text.Json;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Criteria;

// Criteria against one item's field values, for criteria that already passed CriteriaClauseCheck. An empty field never matches
// =, contains, > or <, and always matches <>, notContains and isEmpty; text and tags ignore case, everything else compares exactly.
internal static class CriteriaMatcher
{
    public static bool Matches(CriteriaJson criteria, IFieldValues item, Guid? me)
        => criteria.Match == CriteriaMatch.All
            ? criteria.Clauses.All(c => ClauseMatches(c, item, me))
            : criteria.Clauses.Any(c => ClauseMatches(c, item, me));

    private static bool ClauseMatches(CriteriaClause clause, IFieldValues item, Guid? me)
    {
        var kind = FieldIds.All.First(f => f.Id == clause.Field).Kind;
        var actual = item.Get(clause.Field);
        var empty = IsEmpty(actual);
        return clause.Op switch
        {
            CriteriaOp.IsEmpty => empty,
            CriteriaOp.IsNotEmpty => !empty,
            CriteriaOp.Equal => !empty && Same(kind, actual, clause.Value, me),
            CriteriaOp.NotEqual => empty || !Same(kind, actual, clause.Value, me),
            CriteriaOp.GreaterThan => !empty && Compare(kind, actual, clause.Value) is > 0,
            CriteriaOp.LessThan => !empty && Compare(kind, actual, clause.Value) is < 0,
            CriteriaOp.In => clause.Value.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Null ? empty : !empty && Same(kind, actual, e, me)),
            CriteriaOp.Contains => !empty && Holds(kind, actual, clause.Value),
            CriteriaOp.NotContains => empty || !Holds(kind, actual, clause.Value),
            _ => throw new InvalidOperationException($"Operator {clause.Op} is not a criteria operator."),
        };
    }

    private static bool IsEmpty(JsonElement value)
        => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        || (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0);

    private static bool Same(FieldKind kind, JsonElement actual, JsonElement wanted, Guid? me)
    {
        switch (kind)
        {
            case FieldKind.Number or FieldKind.Points:
                return actual.TryGetDecimal(out var a) && wanted.TryGetDecimal(out var w) && a == w;
            case FieldKind.User or FieldKind.State or FieldKind.Sprint or FieldKind.WorkItem:
                var wantedText = wanted.GetString() == CriteriaClauseCheck.Me ? me?.ToString() : wanted.GetString();
                return Guid.TryParseExact(actual.ValueKind == JsonValueKind.String ? actual.GetString() : null, "D", out var actualId)
                    && Guid.TryParseExact(wantedText, "D", out var wantedId) && actualId == wantedId;
            default:
                var comparison = kind == FieldKind.Text ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
                return actual.ValueKind == JsonValueKind.String && string.Equals(actual.GetString(), wanted.GetString(), comparison);
        }
    }

    private static int? Compare(FieldKind kind, JsonElement actual, JsonElement wanted)
    {
        if (kind == FieldKind.Date)
        {
            return actual.ValueKind == JsonValueKind.String && TryDate(actual.GetString(), out var a) && TryDate(wanted.GetString(), out var w)
                ? a.CompareTo(w)
                : null;
        }
        return actual.TryGetDecimal(out var x) && wanted.TryGetDecimal(out var y) ? x.CompareTo(y) : null;
    }

    private static bool TryDate(string? text, out DateTimeOffset value)
        => DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out value);

    // contains: a substring of text, a whole tag of tags.
    private static bool Holds(FieldKind kind, JsonElement actual, JsonElement wanted)
    {
        var text = wanted.GetString() ?? string.Empty;
        return kind == FieldKind.Tags
            ? actual.ValueKind == JsonValueKind.Array && actual.EnumerateArray().Any(t => t.ValueKind == JsonValueKind.String && string.Equals(t.GetString(), text, StringComparison.OrdinalIgnoreCase))
            : actual.ValueKind == JsonValueKind.String && actual.GetString()!.Contains(text, StringComparison.OrdinalIgnoreCase);
    }
}
