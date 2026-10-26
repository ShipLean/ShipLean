using System.Threading.Channels;
using ShipLean.Contracts.Events;

namespace ShipLean.WebApi.Tests.Taskboard;

// Collects the events a project publishes while a test acts, and waits for one of a kind.
public sealed class TaskboardEventWatch : IDisposable
{
    private static readonly TimeSpan Patience = TimeSpan.FromSeconds(10);

    private readonly Channel<ProjectEvent> _published = Channel.CreateUnbounded<ProjectEvent>();
    private readonly IDisposable _subscription;

    public TaskboardEventWatch(IProjectEvents bus, Guid projectId)
        => _subscription = bus.Subscribe(projectId, e =>
        {
            _published.Writer.TryWrite(e);
            return Task.CompletedTask;
        });

    public async Task<T> NextAsync<T>(Func<T, bool> matches)
        where T : ProjectEvent
    {
        using var timeout = new CancellationTokenSource(Patience);
        while (true)
        {
            var next = await _published.Reader.ReadAsync(timeout.Token);
            if (next is T wanted && matches(wanted))
            {
                return wanted;
            }
        }
    }

    public void Dispose() => _subscription.Dispose();
}
