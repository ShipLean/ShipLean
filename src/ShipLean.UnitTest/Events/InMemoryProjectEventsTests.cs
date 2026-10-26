using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using ShipLean.Application.Events;
using ShipLean.Contracts;
using ShipLean.Contracts.Events;

namespace ShipLean.UnitTest.Events;

[TestFixture]
public class InMemoryProjectEventsTests
{
    private static InMemoryProjectEvents NewBus(ILogger<InMemoryProjectEvents>? logger = null, IHttpContextAccessor? accessor = null)
        => new(logger ?? NullLogger<InMemoryProjectEvents>.Instance, accessor);

    private static Task<List<ProjectEvent>> CollectAsync(IProjectEvents bus, Guid? projectId, int count, out IDisposable subscription)
    {
        var received = new List<ProjectEvent>();
        var done = new TaskCompletionSource<List<ProjectEvent>>(TaskCreationOptions.RunContinuationsAsynchronously);
        subscription = bus.Subscribe(projectId, e =>
        {
            lock (received)
            {
                received.Add(e);
                if (received.Count == count)
                {
                    done.TrySetResult(received);
                }
            }
            return Task.CompletedTask;
        });
        return done.Task;
    }

    [Test]
    [CancelAfter(10000)]
    public async Task Publish_ShouldDeliverInOrderOffRequest()
    {
        using var bus = NewBus();
        var projectId = Guid.NewGuid();
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var received = new List<ProjectEvent>();
        var all = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var subscription = bus.Subscribe(projectId, async e =>
        {
            await release.Task;
            lock (received)
            {
                received.Add(e);
                if (received.Count == 3)
                {
                    all.TrySetResult();
                }
            }
        });
        var sent = new ProjectEvent[] { new ProjectChanged(projectId), new StatesChanged(projectId), new SprintChanged(projectId, Guid.NewGuid()) };

        foreach (var e in sent)
        {
            await bus.PublishAsync(e);
        }
        received.ShouldBeEmpty();
        release.SetResult();
        await all.Task;

        lock (received)
        {
            received.ShouldBe(sent);
        }
    }

    [Test]
    [CancelAfter(10000)]
    public async Task Publish_WhenHandlerThrows_ShouldLogAndContinue()
    {
        var logger = new CapturingLogger();
        using var bus = NewBus(logger);
        var projectId = Guid.NewGuid();
        var second = new TaskCompletionSource<ProjectEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var subscription = bus.Subscribe(projectId, e =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new InvalidOperationException("boom");
            }
            second.TrySetResult(e);
            return Task.CompletedTask;
        });
        var expected = new StatesChanged(projectId);

        await bus.PublishAsync(new ProjectChanged(projectId));
        await bus.PublishAsync(expected);

        (await second.Task).ShouldBe(expected);
        logger.Errors.Single().ShouldBeOfType<InvalidOperationException>().Message.ShouldBe("boom");
    }

    [Test]
    [CancelAfter(10000)]
    public async Task Publish_WhenHandlerThrowsCancellation_ShouldLogAndContinue()
    {
        var logger = new CapturingLogger();
        using var bus = NewBus(logger);
        var projectId = Guid.NewGuid();
        var second = new TaskCompletionSource<ProjectEvent>(TaskCreationOptions.RunContinuationsAsynchronously);
        var calls = 0;
        using var subscription = bus.Subscribe(projectId, e =>
        {
            if (Interlocked.Increment(ref calls) == 1)
            {
                throw new TaskCanceledException("send timed out");
            }
            second.TrySetResult(e);
            return Task.CompletedTask;
        });
        var expected = new StatesChanged(projectId);

        await bus.PublishAsync(new ProjectChanged(projectId));
        await bus.PublishAsync(expected);

        (await second.Task).ShouldBe(expected);
        logger.Errors.Single().ShouldBeOfType<TaskCanceledException>();
    }

    [Test]
    [CancelAfter(10000)]
    public async Task Publish_ShouldStampTheRequestsClientId()
    {
        var context = new DefaultHttpContext();
        context.Request.Headers[ClientHeader.Name] = "tab-1";
        var accessor = new HttpContextAccessor { HttpContext = context };
        using var bus = NewBus(accessor: accessor);
        var projectId = Guid.NewGuid();
        var received = CollectAsync(bus, projectId, 3, out var subscription);
        using var _ = subscription;

        await bus.PublishAsync(new ProjectChanged(projectId));
        context.Request.Headers[ClientHeader.Name] = "  ";
        await bus.PublishAsync(new ProjectChanged(projectId));
        accessor.HttpContext = null;
        await bus.PublishAsync(new ProjectChanged(projectId));

        (await received).Select(e => e.ClientId).ShouldBe(["tab-1", null, null]);
    }

    [Test]
    [CancelAfter(10000)]
    public async Task Subscribe_WhenProjectGiven_ShouldOnlyReceiveThatProject()
    {
        using var bus = NewBus();
        var mine = Guid.NewGuid();
        var received = CollectAsync(bus, mine, 1, out var subscription);
        using var _ = subscription;
        var everything = CollectAsync(bus, null, 2, out var all);
        using var __ = all;

        await bus.PublishAsync(new StatesChanged(Guid.NewGuid()));
        var expected = new StatesChanged(mine);
        await bus.PublishAsync(expected);

        (await received).ShouldBe([expected]);
        (await everything).Count.ShouldBe(2);
    }

    [Test]
    [CancelAfter(10000)]
    public async Task Subscribe_WhenDisposed_ShouldStopDelivery()
    {
        using var bus = NewBus();
        var projectId = Guid.NewGuid();
        var disposedCalls = 0;
        var disposed = bus.Subscribe(projectId, _ =>
        {
            Interlocked.Increment(ref disposedCalls);
            return Task.CompletedTask;
        });
        var live = CollectAsync(bus, projectId, 1, out var liveSubscription);
        using var _ = liveSubscription;
        disposed.Dispose();

        await bus.PublishAsync(new ProjectChanged(projectId));
        await live;

        disposedCalls.ShouldBe(0);
    }

    private sealed class CapturingLogger : ILogger<InMemoryProjectEvents>
    {
        private readonly List<Exception> _errors = [];

        public IReadOnlyList<Exception> Errors => _errors;

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error && exception is not null)
            {
                lock (_errors)
                {
                    _errors.Add(exception);
                }
            }
        }
    }
}
