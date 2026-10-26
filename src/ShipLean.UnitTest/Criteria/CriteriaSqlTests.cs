using System.Text.Json;
using ShipLean.Application.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.Contracts.Criteria;
using ShipLean.TestSupport.Contracts.Users;
using Shouldly;

namespace ShipLean.UnitTest.Criteria;

public sealed class CriteriaSqlTests
{
    private readonly CriteriaEvaluator _evaluator = new(new SettableCurrentUser { Id = Guid.NewGuid() });

    [Test]
    public void Sql_WhenTwoFragmentsCombined_ShouldNotClash()
    {
        var criteria = One(FieldIds.Priority, CriteriaOp.Equal, 1);

        var first = _evaluator.ToSql(criteria, "w");
        var second = _evaluator.ToSql(criteria, "w");

        first.Parameters.Keys.Intersect(second.Parameters.Keys).ShouldBeEmpty();
        first.Parameters.Count.ShouldBe(1);
        second.Where.ShouldNotContain(first.Parameters.Keys.Single());
    }

    [Test]
    public void Sql_WhenValueGiven_ShouldKeepEveryCaseValueOutOfTheText()
    {
        foreach (var testCase in CriteriaCaseFile.Read().Cases.Where(c => c.Valid))
        {
            var sql = _evaluator.ToSql(testCase.Criteria, "w");
            var text = sql.Parameters.Keys.Aggregate(sql.Where, (where, name) => where.Replace("@" + name, string.Empty, StringComparison.Ordinal));

            foreach (var value in testCase.Criteria.Clauses.SelectMany(c => Texts(c.Value)).Where(t => t != "@me"))
            {
                text.ShouldNotContain(value, Case.Sensitive, testCase.Name);
            }
        }
    }

    [Test]
    public void Sql_WhenFieldIdNotInVocabulary_ShouldBeValidation()
    {
        var criteria = One("title; drop table work_item", CriteriaOp.Equal, "x");

        Should.Throw<ApiException>(() => _evaluator.ToSql(criteria, "w")).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public void Sql_WhenFieldIsNotAllowedInCriteria_ShouldBeValidation()
    {
        var criteria = One(FieldIds.Description, CriteriaOp.Equal, "x");

        Should.Throw<ApiException>(() => _evaluator.ToSql(criteria, "w")).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public void Sql_WhenAliasIsNotAnIdentifier_ShouldBeRefused()
    {
        var criteria = One(FieldIds.Priority, CriteriaOp.Equal, 1);

        Should.Throw<ArgumentException>(() => _evaluator.ToSql(criteria, "w; drop table work_item"));
    }

    private static IEnumerable<string> Texts(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => [value.GetString()!],
        JsonValueKind.Array => value.EnumerateArray().SelectMany(Texts),
        _ => [],
    };

    private static CriteriaJson One<T>(string field, CriteriaOp op, T value)
        => new(CriteriaMatch.All, [new CriteriaClause(field, op, JsonSerializer.SerializeToElement(value))]);
}
