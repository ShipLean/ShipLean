using System.Data.Common;
using ShipLean.Application.Fakes.Projects;
using ShipLean.Application.Fakes.Sprints;
using ShipLean.Contracts;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport.Contracts.Sprints;
using ShipLean.UnitTest.Contracts.Projects;

namespace ShipLean.UnitTest.Contracts.Sprints;

public sealed class InMemorySprintStoreContractTests : SprintStoreContract
{
    private InMemoryPlanning _world = null!;

    [SetUp]
    public void CreateWorld() => _world = new InMemoryPlanning();

    protected override ISprintStore CreateStore() => new InMemorySprintStore(_world);

    protected override Task<Guid> ArrangeProjectAsync() => Task.FromResult(_world.AddProject(new string([.. Guid.NewGuid().ToString("N").Where(char.IsLetter).Take(4).Select(char.ToUpperInvariant)]), "Project"));

    protected override Task<Guid> ArrangeMemberAsync(Guid projectId)
    {
        var user = _world.AddUser("Member " + Guid.NewGuid().ToString("N")[..6], $"{Guid.NewGuid():N}@example.test");
        _world.AddMember(projectId, user, ProjectRole.Member);
        return Task.FromResult(user);
    }

    protected override Task<DbTransaction> BeginTransactionAsync() => Task.FromResult<DbTransaction>(new NoOpTransaction());
}
