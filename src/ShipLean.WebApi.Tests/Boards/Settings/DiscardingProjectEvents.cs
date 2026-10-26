using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Boards.Settings;

// An IProjectEvents that drops everything, for contract tests that build a store by hand (event assertions are the API tests').
internal sealed class DiscardingProjectEvents : IProjectEvents
{
    public Task PublishAsync(ProjectEvent projectEvent) => Task.CompletedTask;

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => throw new NotSupportedException();
}
