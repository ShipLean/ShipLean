using System.Collections.Immutable;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Requests;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Events;

// The production bus (one process): one unbounded channel and one pump task per subscriber, so Publish only enqueues and
// never waits for a handler. Disposing a subscription removes it before the next publish and stops its pump.
// The bus is a singleton and IRequestOrigin is scoped, so it reads the request's client header itself on every publish.
public sealed class InMemoryProjectEvents(ILogger<InMemoryProjectEvents> logger, IHttpContextAccessor? httpContext = null)
    : IProjectEvents, IDisposable
{
    private ImmutableList<Subscription> _subscriptions = [];

    public Task PublishAsync(ProjectEvent projectEvent)
    {
        var stamped = projectEvent with { ClientId = projectEvent.ClientId ?? new RequestOrigin(() => httpContext?.HttpContext).ClientId };
        foreach (var subscription in _subscriptions.Where(s => s.Wants(stamped)))
        {
            subscription.Enqueue(stamped);
        }
        return Task.CompletedTask;
    }

    public IDisposable Subscribe(Guid? projectId, Func<ProjectEvent, Task> handler)
    {
        var subscription = new Subscription(projectId, handler, logger, Remove);
        ImmutableInterlocked.Update(ref _subscriptions, list => list.Add(subscription));
        return subscription;
    }

    public void Dispose()
    {
        foreach (var subscription in _subscriptions)
        {
            subscription.Dispose();
        }
    }

    private void Remove(Subscription subscription)
        => ImmutableInterlocked.Update(ref _subscriptions, list => list.Remove(subscription));

    private sealed class Subscription : IDisposable
    {
        private readonly Guid? _projectId;
        // Unbounded: one subscription per relay (the hub's), not per connection; a slow handler holds events, never loses them.
        private readonly Channel<ProjectEvent> _queue = Channel.CreateUnbounded<ProjectEvent>();
        private readonly CancellationTokenSource _stop = new();
        private readonly CancellationToken _token;
        private readonly Action<Subscription> _remove;
        private int _disposed;

        public Subscription(Guid? projectId, Func<ProjectEvent, Task> handler, ILogger logger, Action<Subscription> remove)
        {
            _projectId = projectId;
            _remove = remove;
            _token = _stop.Token;
            _ = Task.Run(() => PumpAsync(handler, logger), _token);
        }

        public bool Wants(ProjectEvent projectEvent) => _projectId == null || _projectId == projectEvent.ProjectId;

        public void Enqueue(ProjectEvent projectEvent) => _queue.Writer.TryWrite(projectEvent);

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
            {
                return;
            }
            _remove(this);
            _stop.Cancel();
            _queue.Writer.TryComplete();
            _stop.Dispose();
        }

        private async Task PumpAsync(Func<ProjectEvent, Task> handler, ILogger logger)
        {
            try
            {
                await foreach (var projectEvent in _queue.Reader.ReadAllAsync(_token))
                {
                    try
                    {
                        await handler(projectEvent);
                    }
                    // A handler's own cancellation (a send timeout, say) is just another failure: only disposal stops delivery.
                    catch (Exception e) when (!_token.IsCancellationRequested)
                    {
                        logger.LogError(e, "A project event handler threw for {EventType}", projectEvent.GetType().Name);
                    }
                }
            }
            catch (OperationCanceledException) when (_token.IsCancellationRequested)
            {
                // Disposed: stop delivering.
            }
        }
    }
}
