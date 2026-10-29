using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.AgentDispatch;

// The one subscription to every project's events that feeds AgentTrigger. The bus delivers off the publishing request and
// logs a handler that throws, so a failure here never reaches a save.
public sealed class AgentDispatchSubscriber(IProjectEvents events, AgentTrigger trigger, ILogger<AgentDispatchSubscriber> logger) : IHostedService
{
    private IDisposable? _subscription;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _subscription = events.Subscribe(null, async projectEvent =>
        {
            if (projectEvent is not WorkItemUpdated)
            {
                return;
            }

            try
            {
                await trigger.HandleAsync(projectEvent, CancellationToken.None);
            }
            catch (Exception failed)
            {
                logger.LogError(failed, "Could not start an agent session for a {Event} event", projectEvent.GetType().Name);
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
