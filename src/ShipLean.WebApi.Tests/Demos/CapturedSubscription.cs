using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Demos;

// A bus that only hands back the handler a subscriber registered, so a test can call that handler directly and see what it does with a throw.
internal sealed class CapturedSubscription : IProjectEvents
{
    public Func<ProjectEvent, Task>? Handler { get; private set; }

    public Task PublishAsync(ProjectEvent projectEvent) => throw new NotSupportedException();

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler)
    {
        Handler = handler;
        return new Nothing();
    }

    private sealed class Nothing : IDisposable
    {
        public void Dispose()
        {
        }
    }
}
