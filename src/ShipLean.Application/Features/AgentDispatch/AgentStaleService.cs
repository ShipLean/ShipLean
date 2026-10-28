using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShipLean.Application.Features.AgentDispatch;

// The hosted loop around AgentStaleSweeper, every 5 minutes. Like AgentDeliveryService it starts without the starter's
// execution context, and a pass that fails is logged rather than allowed to stop the host.
public sealed class AgentStaleService(AgentStaleSweeper sweeper, TimeProvider time, ILogger<AgentStaleService> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(5);

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Task started;
        using (ExecutionContext.SuppressFlow())
        {
            started = base.StartAsync(cancellationToken);
        }

        return started;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every, time);
        try
        {
            do
            {
                await PassAsync(stoppingToken);
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // The host is stopping.
        }
    }

    private async Task PassAsync(CancellationToken stoppingToken)
    {
        try
        {
            await sweeper.SweepAsync(stoppingToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "The stale agent session sweep failed");
        }
    }
}
