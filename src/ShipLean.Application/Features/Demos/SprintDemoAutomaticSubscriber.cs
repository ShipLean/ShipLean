using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.Demos;

// On SprintClosed, starts the sprint's automatic demo. ISprintDemos is scoped and this is a singleton, so each event gets a scope of its own.
// The bus delivers off the publishing request; a failure is logged here and never reaches the close.
public sealed class SprintDemoAutomaticSubscriber(IProjectEvents events, IServiceScopeFactory scopes, ILogger<SprintDemoAutomaticSubscriber> logger) : IHostedService
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = events.Subscribe(null, async projectEvent =>
        {
            if (projectEvent is not SprintClosed closed)
            {
                return;
            }

            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ISprintDemos>()
                    .StartAutomaticAsync(closed.ProjectId, closed.SprintId, closed.ClosedBy, CancellationToken.None);
            }
            catch (Exception failed)
            {
                logger.LogError(failed, "Could not start the automatic demo for sprint {SprintId}", closed.SprintId);
            }
        });
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        return Task.CompletedTask;
    }
}
