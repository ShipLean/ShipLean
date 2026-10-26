using System.Text.Json;
using System.Text.RegularExpressions;
using ShipLean.Contracts;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Criteria;

// The real ICriteriaEvaluator: in memory over IFieldValues, and as a parameterised WHERE over work_item.
// Using ToSql: the text refers to its values as "@c7_0", "@c7_1", ...; CriteriaSql.Parameters holds the same names without the "@".
// Add every entry to one Dapper DynamicParameters, then AND or OR the fragments (a lane's own criteria and the board filter) into
// one query. Each call takes a new prefix ("c7_") from a counter on this evaluator instance, so fragments from the same instance
// never clash (the registered singleton is that instance; two instances both start at c1_). The counter is unsigned and wraps to 0
// after 2^32 calls. The "@p0" example in the CriteriaSql contract comment is only an example of the shape; these names are the real ones.
// "@me" binds to ICurrentUser at ToSql time, so don't cache a fragment across users.
public sealed partial class CriteriaEvaluator(ICurrentUser currentUser) : ICriteriaEvaluator
{
    private uint _calls;

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
        Require(criteria);
        return CriteriaMatcher.Matches(criteria, item, Me(criteria));
    }

    public CriteriaSql ToSql(CriteriaJson criteria, string alias)
    {
        Require(criteria);
        if (!AliasPattern().IsMatch(alias))
        {
            throw new ArgumentException("The alias must be a plain identifier.", nameof(alias));
        }
        var prefix = $"c{Interlocked.Increment(ref _calls)}_";
        return CriteriaSqlBuilder.Build(criteria, alias, prefix, Me(criteria));
    }

    private void Require(CriteriaJson criteria)
    {
        var errors = Validate(criteria);
        if (errors.Count > 0)
        {
            throw ApiException.Validation(errors[0].Message);
        }
    }

    // The current user is read only when a user clause's value is "@me" (or lists it), never for text that merely contains it.
    private Guid? Me(CriteriaJson criteria)
        => criteria.Clauses.Any(IsMe) ? currentUser.Id : null;

    private static bool IsMe(CriteriaClause clause)
        => clause.Value.ValueKind switch
        {
            JsonValueKind.String => clause.Value.GetString() == CriteriaClauseCheck.Me,
            JsonValueKind.Array => clause.Value.EnumerateArray().Any(e => e.ValueKind == JsonValueKind.String && e.GetString() == CriteriaClauseCheck.Me),
            _ => false,
        } && FieldIds.All.First(f => f.Id == clause.Field).Kind == FieldKind.User;

    [GeneratedRegex("^[A-Za-z_][A-Za-z0-9_]*$")]
    private static partial Regex AliasPattern();
}
