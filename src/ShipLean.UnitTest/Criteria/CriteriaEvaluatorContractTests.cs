using ShipLean.Application.Criteria;
using ShipLean.Contracts.Criteria;
using ShipLean.Contracts.Persisted;
using ShipLean.TestSupport.Contracts.Criteria;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.UnitTest.Criteria;

// The real evaluator in memory. The SQL it writes runs against Postgres in the WebApi tests; here it must only be written.
public sealed class CriteriaEvaluatorContractTests : CriteriaEvaluatorContract
{
    private readonly SettableCurrentUser _currentUser = new();
    private CriteriaEvaluator _evaluator = null!;

    [SetUp]
    public void CreateEvaluator() => _evaluator = new CriteriaEvaluator(_currentUser);

    protected override ICriteriaEvaluator Evaluator => _evaluator;

    protected override IReadOnlySet<CriteriaOp> MatchedOperators { get; } = new HashSet<CriteriaOp>(Enum.GetValues<CriteriaOp>());

    protected override bool SupportsSql => true;

    protected override bool RunsSql => false;

    protected override void ActAs(Guid userId) => _currentUser.Id = userId;
}
