using ShipLean.Application.Fakes.Criteria;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.Contracts.Criteria;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Criteria;

public sealed class InMemoryCriteriaEvaluatorContractTests : CriteriaEvaluatorContract
{
    private readonly SettableCurrentUser _currentUser = new();
    private InMemoryCriteriaEvaluator _evaluator = null!;

    [SetUp]
    public void CreateEvaluator() => _evaluator = new InMemoryCriteriaEvaluator(_currentUser);

    protected override ICriteriaEvaluator Evaluator => _evaluator;

    protected override IReadOnlySet<CriteriaOp> MatchedOperators { get; } = new HashSet<CriteriaOp>
    {
        CriteriaOp.Equal, CriteriaOp.NotEqual, CriteriaOp.In, CriteriaOp.IsEmpty, CriteriaOp.IsNotEmpty,
    };

    protected override bool SupportsSql => false;

    protected override void ActAs(Guid userId) => _currentUser.Id = userId;
}
