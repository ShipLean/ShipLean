using ShipLean.Contracts.Persisted;

namespace ShipLean.Contracts.Criteria;

public interface ICriteriaEvaluator
{
    // Unknown field, operator the field does not allow, value of the wrong type, no clauses. Empty when valid.
    IReadOnlyList<CriteriaValidationError> Validate(CriteriaJson criteria);

    // "@me" in a clause is ICurrentUser.Id. An empty or missing field never matches =, contains, >, <, or in without a null element;
    // it matches <>, notContains, isEmpty, and in with a null element (so "tags notContains Blocked" holds for an item with no tags). Text and tags compare ignoring case; state categories and types compare exactly (camelCase). Throws ApiException.Validation (the criteria are invalid).
    bool Matches(CriteriaJson criteria, IFieldValues item);

    // A WHERE fragment over the work_item table under the alias. Throws ApiException.Validation (the criteria are invalid).
    CriteriaSql ToSql(CriteriaJson criteria, string alias);
}
