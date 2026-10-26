using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.WorkItems.Store;

// An event bus that keeps what was published, in order, for assertions.
public sealed class RecordingProjectEvents : IProjectEvents
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

        return Task.CompletedTask;
    }

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler) => throw new NotSupportedException();
}
