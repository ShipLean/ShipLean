using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShipLean.Application.Features.IssueIntake.Rounds;

// The hosted loop around RoundJobWorker: every 15 seconds it runs round jobs until none is due. Like AgentStaleService it starts
// without the starter's execution context, and a pass that fails is logged rather than allowed to stop the host.
public sealed class RoundJobService(RoundJobWorker worker, TimeProvider time, ILogger<RoundJobService> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(15);

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
            while (await worker.RunNextAsync(stoppingToken))
            {
                // One job at a time until none is due.
            }
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "The round job pass failed");
        }
    }
}
