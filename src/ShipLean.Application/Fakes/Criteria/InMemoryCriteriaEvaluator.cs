using System.Text.Json;
using ShipLean.Application.Criteria;
using ShipLean.Contracts;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Fakes.Criteria;

// ICriteriaEvaluator in memory. Validate covers every operator; Matches only =, <>, in, isEmpty and isNotEmpty (anything
// else throws NotSupportedException), and ToSql is not supported.
public sealed class InMemoryCriteriaEvaluator(ICurrentUser currentUser) : ICriteriaEvaluator
{
    public IReadOnlyList<CriteriaValidationError> Validate(CriteriaJson criteria)
    {
        if (criteria.Clauses.Count == 0)
        {
            return [new CriteriaValidationError(null, string.Empty, "noClauses", "Add at least one clause, or remove the criteria.")];
        }
        return criteria.Clauses.Select((clause, index) => CriteriaClauseCheck.Check(index, clause)).OfType<CriteriaValidationError>().ToList();
    }

    public bool Matches(CriteriaJson criteria, IFieldValues item)
    {
        var errors = Validate(criteria);
        if (errors.Count > 0)
        {
            throw ApiException.Validation(errors[0].Message);
        }
        var unsupported = criteria.Clauses.FirstOrDefault(c => c.Op is not (CriteriaOp.Equal or CriteriaOp.NotEqual or CriteriaOp.In or CriteriaOp.IsEmpty or CriteriaOp.IsNotEmpty));
        if (unsupported is not null)
        {
            throw new NotSupportedException($"The in-memory evaluator does not match {unsupported.Op}.");
        }
        return criteria.Match == CriteriaMatch.All
            ? criteria.Clauses.All(c => ClauseMatches(c, item))
            : criteria.Clauses.Any(c => ClauseMatches(c, item));
    }

    public CriteriaSql ToSql(CriteriaJson criteria, string alias)
        => throw new NotSupportedException("The in-memory evaluator does not write SQL.");

    private bool ClauseMatches(CriteriaClause clause, IFieldValues item)
    {
        var actual = item.Get(clause.Field);
        return clause.Op switch
        {
            CriteriaOp.IsEmpty => IsEmpty(actual),
            CriteriaOp.IsNotEmpty => !IsEmpty(actual),
            CriteriaOp.Equal => !IsEmpty(actual) && Same(actual, clause.Value),
            CriteriaOp.NotEqual => IsEmpty(actual) || !Same(actual, clause.Value),
            CriteriaOp.In => clause.Value.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.Null ? IsEmpty(actual) : !IsEmpty(actual) && Same(actual, e)),
            _ => throw new InvalidOperationException($"Operator {clause.Op} was checked before matching."),
        };
    }

    private static bool IsEmpty(JsonElement value)
        => value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined
        || (value.ValueKind == JsonValueKind.Array && value.GetArrayLength() == 0);

    private bool Same(JsonElement actual, JsonElement wanted)
    {
        if (actual.ValueKind == JsonValueKind.Number && wanted.ValueKind == JsonValueKind.Number)
        {
            return actual.GetDecimal() == wanted.GetDecimal();
        }
        if (actual.ValueKind == JsonValueKind.String && wanted.ValueKind == JsonValueKind.String)
        {
            var text = wanted.GetString() == CriteriaClauseCheck.Me ? currentUser.Id.ToString() : wanted.GetString();
            return string.Equals(actual.GetString(), text, StringComparison.OrdinalIgnoreCase);
        }
        return false;
    }
}
