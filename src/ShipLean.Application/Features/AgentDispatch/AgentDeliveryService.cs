using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShipLean.Application.Features.AgentDispatch;

// The hosted loop around AgentDeliverer. Everything it works from is in the database, so a restart resumes where the last
// process stopped (a row it had claimed is free again when its lease runs out). It starts without the starter's execution
// context, so nothing it does carries the request or user of whoever started the host (as ImportRunner).
public sealed class AgentDeliveryService(AgentDeliverer deliverer, TimeProvider time, ILogger<AgentDeliveryService> logger) : BackgroundService
{
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(3);

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
        using var timer = new PeriodicTimer(Poll, time);
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

    // An exception out of a BackgroundService would stop the host, so a pass that blows up is logged and the next one runs.
    private async Task PassAsync(CancellationToken stoppingToken)
    {
        try
        {
            await deliverer.RunDueAsync(stoppingToken);
            await deliverer.CancelUnansweredStopsAsync(stoppingToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "An agent delivery pass failed");
        }
    }
}
