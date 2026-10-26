using ShipLean.Application.Fakes.Metrics;
using ShipLean.Application.Fakes.Projects;
using ShipLean.Contracts.Refs;
using ShipLean.Contracts.Sprints;
using ShipLean.TestSupport.Contracts.Metrics;

namespace ShipLean.UnitTest.Contracts.Metrics;

public sealed class InMemorySprintItemOutcomesContractTests : SprintItemOutcomesContract
{
    private InMemoryPlanning _world = null!;

    [SetUp]
    public void CreateWorld() => _world = new InMemoryPlanning();

    protected override ISprintMetrics CreateMetrics() => new InMemorySprintMetrics(_world);

    protected override Task<Guid> ArrangeProjectAsync() => Task.FromResult(_world.AddProject("MET", "Metrics"));

    protected override Task<Guid> ArrangeSprintAsync(Guid projectId, string name, DateOnly start, DateOnly end) => Task.FromResult(_world.AddSprint(projectId, name, start, end));

    protected override Task<Guid> ArrangeItemAsync(Guid projectId, WorkItemTypeId type, decimal? points, Guid? sprintId, bool addedAfterStart, DateOnly? completedOn)
        => Task.FromResult(_world.AddItem(projectId, type, points, sprintId, addedAfterStart, completedOn));

    protected override Task MoveItemAsync(Guid itemId, Guid? toSprintId, DateOnly on)
    {
        _world.MoveItem(itemId, toSprintId, on);
        return Task.CompletedTask;
    }

    protected override Task CompleteItemAsync(Guid itemId, DateOnly on)
    {
        _world.CompleteItem(itemId, on);
        return Task.CompletedTask;
    }

    protected override Task ReopenItemAsync(Guid itemId, DateOnly on)
    {
        _world.ReopenItem(itemId, on);
        return Task.CompletedTask;
    }

    protected override Task RemoveItemAsync(Guid itemId, DateOnly on)
    {
        _world.RemoveItem(itemId, on);
        return Task.CompletedTask;
    }
}
