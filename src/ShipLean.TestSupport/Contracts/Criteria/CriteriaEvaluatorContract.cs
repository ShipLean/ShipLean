using System.Text.Json;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;
using Shouldly;

namespace ShipLean.TestSupport.Contracts.Criteria;

// Behaviour every ICriteriaEvaluator has, driven by criteria-contract-cases.json. A derived fixture builds the evaluator
// (acting as CaseFile.Me) in [SetUp] and says what it supports; an implementation that does not support something must throw
// NotSupportedException for it, which the fixture asserts.
[TestFixture]
public abstract class CriteriaEvaluatorContract
{
    protected abstract ICriteriaEvaluator Evaluator { get; }

    // The operators Matches handles; Validate always covers all of them.
    protected abstract IReadOnlySet<CriteriaOp> MatchedOperators { get; }

    protected abstract bool SupportsSql { get; }

    // False for a fixture with no database: ToSql must still write a fragment for every valid case, but nothing runs it.
    protected virtual bool RunsSql => SupportsSql;

    // Runs the WHERE fragment that ToSql wrote for an item of the case file and says whether the item matches. Items use seed ids
    // (states, sprints, work items, users), so a Postgres leg can run against the seed. Called only when SupportsSql.
    protected virtual Task<bool> MatchesInSqlAsync(CriteriaSql sql, JsonElement item)
        => throw new NotSupportedException("An implementation that supports SQL runs the fragment against the database.");

    // Makes "@me" mean this user.
    protected abstract void ActAs(Guid userId);

    private static CriteriaCaseFile Cases { get; } = CriteriaCaseFile.Read();

    public static IEnumerable<string> CaseNames => Cases.Cases.Select(c => c.Name);

    [SetUp]
    public void ActAsTheCaseFileUser() => ActAs(Cases.Me);

    [TestCaseSource(nameof(CaseNames))]
    public async Task Case_ShouldValidateAndMatchAsTheCaseFileSays(string name)
    {
        var testCase = Cases.Cases.Single(c => c.Name == name);

        Evaluator.Validate(testCase.Criteria).Count.ShouldBe(testCase.Valid ? 0 : 1, name);
        if (!testCase.Valid)
        {
            Should.Throw<ApiException>(() => Evaluator.Matches(testCase.Criteria, new JsonFieldValues(testCase.Item))).Code.ShouldBe(ApiErrorCodes.Validation);
            if (SupportsSql)
            {
                Should.Throw<ApiException>(() => Evaluator.ToSql(testCase.Criteria, "w")).Code.ShouldBe(ApiErrorCodes.Validation);
            }
            return;
        }

        if (testCase.Criteria.Clauses.All(c => MatchedOperators.Contains(c.Op)))
        {
            Evaluator.Matches(testCase.Criteria, new JsonFieldValues(testCase.Item)).ShouldBe(testCase.Matches!.Value, name);
        }
        else
        {
            Should.Throw<NotSupportedException>(() => Evaluator.Matches(testCase.Criteria, new JsonFieldValues(testCase.Item)));
        }
        if (SupportsSql)
        {
            var sql = Evaluator.ToSql(testCase.Criteria, "w");
            if (RunsSql)
            {
                (await MatchesInSqlAsync(sql, testCase.Item)).ShouldBe(testCase.Matches!.Value, $"{name} (SQL)");
            }
        }
    }

    [Test]
    public void CaseFile_ShouldCoverEveryPinnedOperatorAndInvalidCases()
    {
        var operators = Cases.Cases.SelectMany(c => c.Criteria.Clauses).Select(c => c.Op).Distinct();

        operators.ShouldBe(Enum.GetValues<CriteriaOp>(), ignoreOrder: true);
        Cases.Cases.Count(c => !c.Valid).ShouldBeGreaterThan(5);
    }

    [Test]
    public void Evaluate_WhenUnknownField_ShouldBeInvalid()
    {
        var criteria = Criteria("colour", CriteriaOp.Equal, "red");

        var errors = Evaluator.Validate(criteria);

        var error = errors.ShouldHaveSingleItem();
        error.ClauseIndex.ShouldBe(0);
        error.Field.ShouldBe("colour");
    }

    [Test]
    public void Validate_WhenOneClauseOfTwoIsInvalid_ShouldPointAtThatClause()
    {
        var criteria = new CriteriaJson(CriteriaMatch.All,
        [
            new CriteriaClause(FieldIds.Priority, CriteriaOp.Equal, JsonSerializer.SerializeToElement(1)),
            new CriteriaClause(FieldIds.Priority, CriteriaOp.Contains, JsonSerializer.SerializeToElement("1")),
        ]);

        Evaluator.Validate(criteria).ShouldHaveSingleItem().ClauseIndex.ShouldBe(1);
    }

    [Test]
    public void Matches_WhenMeIsAnotherUser_ShouldNotMatchTheCaseFileUser()
    {
        var criteria = Criteria(FieldIds.Assignee, CriteriaOp.Equal, "@me");
        var item = new JsonFieldValues(JsonSerializer.SerializeToElement(new Dictionary<string, string> { [FieldIds.Assignee] = Cases.Me.ToString() }));

        ActAs(Guid.NewGuid());

        Evaluator.Matches(criteria, item).ShouldBeFalse();
    }

    [Test]
    public void Sql_WhenValueGiven_ShouldBeParameterised()
    {
        var criteria = Criteria(FieldIds.Title, CriteriaOp.Equal, "x'; drop table work_item; --");
        if (!SupportsSql)
        {
            Should.Throw<NotSupportedException>(() => Evaluator.ToSql(criteria, "w"));
            return;
        }

        var sql = Evaluator.ToSql(criteria, "w");

        sql.Where.ShouldNotContain("drop table");
        sql.Where.ShouldContain("w.");
        sql.Parameters.Values.Select(v => v?.ToString()).ShouldContain(v => v != null && v.Contains("drop table"));
        sql.Parameters.Keys.ShouldAllBe(k => sql.Where.Contains(k));
    }

    [Test]
    public void Sql_WhenCriteriaInvalid_ShouldBeValidation()
    {
        var criteria = Criteria("colour", CriteriaOp.Equal, "red");
        if (!SupportsSql)
        {
            Should.Throw<NotSupportedException>(() => Evaluator.ToSql(criteria, "w"));
            return;
        }

        Should.Throw<ApiException>(() => Evaluator.ToSql(criteria, "w")).Code.ShouldBe(ApiErrorCodes.Validation);
    }

    private static CriteriaJson Criteria(string field, CriteriaOp op, string value)
        => new(CriteriaMatch.All, [new CriteriaClause(field, op, JsonSerializer.SerializeToElement(value))]);
}
