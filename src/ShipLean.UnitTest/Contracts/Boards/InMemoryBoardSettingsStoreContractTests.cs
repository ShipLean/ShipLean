using System.Data;
using System.Data.Common;
using ShipLean.Application.Fakes.Boards;
using ShipLean.Application.Fakes.Criteria;
using ShipLean.Contracts.Boards;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport.Contracts.Boards;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Boards;

public sealed class InMemoryBoardSettingsStoreContractTests : BoardSettingsStoreContract
{
    private InMemoryBoardSettingsStore _store = null!;

    [SetUp]
    public void CreateStore() => _store = new InMemoryBoardSettingsStore(TimeProvider.System, new InMemoryCriteriaEvaluator(new SettableCurrentUser()));

    protected override IBoardSettingsStore Store => _store;

    protected override Task<Guid> ArrangeProjectAsync(IReadOnlyDictionary<WorkItemTypeId, IReadOnlyList<StateRef>> statesByType)
    {
        var projectId = Guid.NewGuid();
        _store.AddProject(projectId, statesByType);
        return Task.FromResult(projectId);
    }

    protected override Task ArrangeStateAsync(Guid projectId, WorkItemTypeId type, StateRef state)
    {
        _store.AddState(projectId, type, state);
        return Task.CompletedTask;
    }

    protected override Task ArrangeCardInColumnAsync(Guid boardId, string columnId)
    {
        _store.MarkColumnHoldsCards(boardId, columnId);
        return Task.CompletedTask;
    }

    protected override async Task InTransactionAsync(Func<DbTransaction, Task> action)
    {
        using var transaction = new NoConnectionTransaction();
        await action(transaction);
    }

    // The fake ignores the transaction, so this one has no connection behind it.
    private sealed class NoConnectionTransaction : DbTransaction
    {
        public override IsolationLevel IsolationLevel => IsolationLevel.ReadCommitted;

        protected override DbConnection? DbConnection => null;

        public override void Commit()
        {
        }

        public override void Rollback()
        {
        }
    }
}
