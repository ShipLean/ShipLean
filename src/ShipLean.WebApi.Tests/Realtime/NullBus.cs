using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Realtime;

// A bus that drops everything published on it.
internal sealed class NullBus : IProjectEvents
{
    public Task PublishAsync(ProjectEvent projectEvent) => Task.CompletedTask;

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => throw new NotSupportedException();
}
