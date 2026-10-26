using ShipLean.Contracts.WorkItems;
using ShipLean.TestSupport.Contracts.WorkItems;

namespace ShipLean.UnitTest.Contracts.WorkItems;

[TestFixture]
public class InMemoryWorkItemImportContractTests : WorkItemImportContract
{
    private ShipLean.Application.Fakes.WorkItems.InMemoryWorkItemStore? _store;

    protected override IWorkItemStore CreateStore(IWorkItemRules rules, Guid actorId)
    {
        _store = InMemoryWorkItemArrangement.CreateStore(rules, actorId);
        return _store;
    }

    protected override Task<ArrangedProject> ArrangeProjectAsync()
        => Task.FromResult(InMemoryWorkItemArrangement.Arrange(_store!));
}
