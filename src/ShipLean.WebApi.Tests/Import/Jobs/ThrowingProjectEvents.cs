using ShipLean.Contracts.Events;
using ShipLean.Contracts.Persisted;

namespace ShipLean.WebApi.Tests.Import.Jobs;

// The event bus, except that once armed publishing the event that says an import is running throws: a dependency that fails inside a run.
internal sealed class ThrowingProjectEvents(IProjectEvents inner) : IProjectEvents, IDisposable
{
    private volatile bool _armed;

    public bool Armed
    {
        get => _armed;
        set => _armed = value;
    }

    public Task PublishAsync(ProjectEvent projectEvent)
        => _armed && projectEvent is ImportProgress { State: ImportJobState.Running } ? throw new InvalidOperationException("The event bus is down.") : inner.PublishAsync(projectEvent);

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => inner.Subscribe(projectId, handler);

    public void Dispose() => (inner as IDisposable)?.Dispose();
}
