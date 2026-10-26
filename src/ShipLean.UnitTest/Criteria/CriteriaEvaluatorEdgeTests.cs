using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using ShipLean.Application.Criteria;
using ShipLean.Contracts;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.Contracts.Criteria;
using Shouldly;

namespace ShipLean.UnitTest.Criteria;

public sealed partial class CriteriaEvaluatorEdgeTests
{
    private readonly CriteriaEvaluator _evaluator = new(new NoUser());

    [Test]
    public void Sql_WhenCounterWraps_ShouldStillNameValidParameters()
    {
        typeof(CriteriaEvaluator).GetField("_calls", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(_evaluator, uint.MaxValue);
        var criteria = One(FieldIds.Priority, CriteriaOp.Equal, 1);

        var last = _evaluator.ToSql(criteria, "w");
        var wrapped = _evaluator.ToSql(criteria, "w");

        foreach (var sql in new[] { last, wrapped })
        {
            sql.Parameters.Keys.ShouldAllBe(name => ParameterName().IsMatch(name));
            var placeholders = Placeholder().Matches(sql.Where).Select(m => m.Groups[1].Value).ToList();
            placeholders.ShouldNotBeEmpty();
            placeholders.ShouldAllBe(name => ParameterName().IsMatch(name));
        }
        last.Parameters.Keys.Intersect(wrapped.Parameters.Keys).ShouldBeEmpty();
    }

    [Test]
    public void Evaluate_WhenTextOnlyContainsMe_ShouldNotReadTheCurrentUser()
    {
        var criteria = One(FieldIds.Title, CriteriaOp.Contains, "ask @me about it");
        var item = new JsonFieldValues(JsonSerializer.SerializeToElement(new { title = "Please ask @me about it" }));

        _evaluator.Matches(criteria, item).ShouldBeTrue();
        _evaluator.ToSql(criteria, "w").Parameters.ShouldNotBeEmpty();
    }

    [Test]
    public void Evaluate_WhenTextFieldIsTheWordMe_ShouldNotReadTheCurrentUser()
    {
        var criteria = One(FieldIds.Title, CriteriaOp.Equal, "@me");
        var item = new JsonFieldValues(JsonSerializer.SerializeToElement(new { title = "@me" }));

        _evaluator.Matches(criteria, item).ShouldBeTrue();
        _evaluator.ToSql(criteria, "w").Parameters.ShouldNotBeEmpty();
    }

    [Test]
    public void Evaluate_WhenUserClauseIsMe_ShouldReadTheCurrentUser()
    {
        var criteria = One(FieldIds.Assignee, CriteriaOp.Equal, "@me");

        Should.Throw<InvalidOperationException>(() => _evaluator.ToSql(criteria, "w"));
        Should.Throw<InvalidOperationException>(() => _evaluator.Matches(criteria, new JsonFieldValues(JsonSerializer.SerializeToElement(new { assignee = Guid.NewGuid() }))));
    }

    [TestCase(FieldIds.Type, "bug", "Bug")]
    [TestCase(FieldIds.StateCategory, "inProgress", "inprogress")]
    public void Evaluate_WhenTypeOrCategoryDiffersInCase_ShouldNotMatch(string field, string wanted, string actual)
    {
        var criteria = One(field, CriteriaOp.Equal, wanted);
        var item = new JsonFieldValues(JsonSerializer.SerializeToElement(new Dictionary<string, string> { [field] = actual }));

        _evaluator.Matches(criteria, item).ShouldBeFalse();
    }

    private static CriteriaJson One<T>(string field, CriteriaOp op, T value)
        => new(CriteriaMatch.All, [new CriteriaClause(field, op, JsonSerializer.SerializeToElement(value))]);

    [GeneratedRegex(@"^c\d+_\d+$")]
    private static partial Regex ParameterName();

    [GeneratedRegex(@"@(\S+?)\)")]
    private static partial Regex Placeholder();

    private sealed class NoUser : ICurrentUser
    {
        public Guid Id => throw new InvalidOperationException("No signed-in user.");
    }
}
