using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShipLean.Application.Features.WorkItemDetails.Attachments;

// The hosted loop around ArchivedVideoSweeper, every 15 minutes, sweeping until a pass finds nothing. Like AgentStaleService it starts
// without the starter's execution context, and a pass that fails is logged rather than allowed to stop the host.
public sealed class ArchivedVideoService(ArchivedVideoSweeper sweeper, TimeProvider time, ILogger<ArchivedVideoService> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(15);

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
            int deleted;
            do
            {
                deleted = await sweeper.SweepAsync(stoppingToken);
            }
            while (deleted > 0);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "The archived video sweep failed");
        }
    }
}
