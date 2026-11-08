using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShipLean.Application.Features.IssueIntake.Outbox;

// The hosted loop around GitHubOutboxDeliverer. Everything it works from is in github_outbox, so a restart resumes where the last
// process stopped (an entry it had claimed is free again when its lease runs out). It starts without the starter's execution
// context, as AgentDeliveryService does.
public sealed class GitHubOutboxDeliveryService(GitHubOutboxDeliverer deliverer, TimeProvider time, ILogger<GitHubOutboxDeliveryService> logger) : BackgroundService
{
    private static readonly TimeSpan Poll = TimeSpan.FromSeconds(5);

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

    // An exception out of a BackgroundService would stop the host, so a pass that blows up is logged and the next one runs; the
    // entry it held is claimed again when its lease runs out.
    private async Task PassAsync(CancellationToken stoppingToken)
    {
        try
        {
            await deliverer.RunDueAsync(stoppingToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "A GitHub outbox delivery pass failed");
        }
    }
}
