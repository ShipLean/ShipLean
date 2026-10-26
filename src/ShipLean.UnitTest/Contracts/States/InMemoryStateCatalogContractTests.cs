using System.Data.Common;
using ShipLean.Application.Fakes.Projects;
using ShipLean.Application.Fakes.States;
using ShipLean.Contracts.Projects;
using ShipLean.TestSupport.Contracts.States;
using ShipLean.UnitTest.Contracts.Projects;

namespace ShipLean.UnitTest.Contracts.States;

public sealed class InMemoryStateCatalogContractTests : StateCatalogContract
{
    private InMemoryPlanning _world = null!;

    [SetUp]
    public void CreateWorld() => _world = new InMemoryPlanning();

    protected override IStateCatalog CreateCatalog() => new InMemoryStateCatalog(_world);

    protected override Task<Guid> ArrangeProjectAsync(bool withDefaultStates)
        => Task.FromResult(_world.AddProject(new string([.. Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(4).Select(char.ToUpperInvariant)]), "Project", withDefaultStates));

    protected override Task<DbTransaction> BeginTransactionAsync() => Task.FromResult<DbTransaction>(new NoOpTransaction());

    protected override Task<Guid> ArrangeItemAsync(Guid projectId, Guid stateId) => Task.FromResult(_world.AddItemInState(projectId, stateId));

    protected override Task<Guid> ReadItemStateAsync(Guid itemId) => Task.FromResult(_world.ItemState(itemId));
}
