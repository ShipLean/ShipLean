using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Demos;

// The host's real bus, with each publish noted synchronously first, so a test reads what was published without waiting on the bus's asynchronous delivery.
internal sealed class TeeProjectEvents(IProjectEvents inner) : IProjectEvents, IDisposable
{
    private readonly List<ProjectEvent> _published = [];

    public IReadOnlyList<ProjectEvent> Published
    {
        get
        {
            lock (_published)
            {
                return [.. _published];
            }
        }
    }

    public Task PublishAsync(ProjectEvent projectEvent)
    {
        lock (_published)
        {
            _published.Add(projectEvent);
        }

        return inner.PublishAsync(projectEvent);
    }

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => inner.Subscribe(projectId, handler);

    public void Dispose() => (inner as IDisposable)?.Dispose();
}
