using ShipLean.Application.Fakes.Projects;
using ShipLean.Contracts;
using ShipLean.Contracts.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.TestSupport.Contracts.Projects;
using ShipLean.TestSupport.Contracts.Users;

namespace ShipLean.UnitTest.Contracts.Projects;

public sealed class InMemoryProjectFactoryContractTests : ProjectFactoryContract
{
    private InMemoryPlanning _world = null!;

    [SetUp]
    public void CreateWorld() => _world = new InMemoryPlanning();

    protected override IProjectFactory CreateFactory(Guid actorId) => new InMemoryProjectFactory(_world, new SettableCurrentUser { Id = actorId });

    protected override Task<Guid> ArrangeUserAsync(string email) => Task.FromResult(_world.AddUser(email.Split('@')[0], email));

    protected override Task<Guid> ArrangeProjectAsync(string key) => Task.FromResult(_world.AddProject(key, key));

    protected override Task<IReadOnlyList<(Guid UserId, ProjectRole Role)>> ReadMembersAsync(Guid projectId)
        => Task.FromResult(_world.MembersOf(projectId));

    protected override Task<Guid> ArrangeSourceProjectAsync(string key, string stateName)
    {
        var project = _world.AddProject(key, key);
        _world.AddState(project, WorkItemTypeId.Story, stateName, StateCategory.InProgress);
        _world.AddItem(project, WorkItemTypeId.Story, 3);
        return Task.FromResult(project);
    }

    protected override Task<IReadOnlyList<(WorkItemTypeId Type, string Name)>> ReadStatesAsync(Guid projectId) => Task.FromResult(_world.StateNames(projectId));

    protected override Task<int> CountItemsAsync(Guid projectId) => Task.FromResult(_world.ItemCount(projectId));

    protected override Task<int> CountSprintsAsync(Guid projectId) => Task.FromResult(_world.SprintCount(projectId));
}
