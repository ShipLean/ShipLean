using Microsoft.Extensions.Hosting;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.Demos;

// TODO: the real subscriber replaces this: on SprintClosed it starts the automatic run in a fresh scope.
public sealed class SprintDemoAutomaticSubscriber(IProjectEvents events) : IHostedService
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = events.Subscribe(null, _ => Task.CompletedTask);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }
}
