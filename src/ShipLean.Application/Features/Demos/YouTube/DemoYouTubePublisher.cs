using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShipLean.Application.Features.Demos.YouTube;

// The hosted loop that forwards kept demo videos to YouTube, every 30 seconds. A pass that fails is logged rather than allowed to stop the host.
public sealed class DemoYouTubePublisher(TimeProvider time, ILogger<DemoYouTubePublisher> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(30);

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

    // TODO: the real pass claims the pending rows and uploads them; it returns how many it published.
    public Task<int> PublishDueAsync(CancellationToken cancellationToken = default)
    {
        logger.LogDebug("No demo videos are forwarded to YouTube yet");
        return Task.FromResult(0);
    }

    private async Task PassAsync(CancellationToken stoppingToken)
    {
        try
        {
            await PublishDueAsync(stoppingToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "Forwarding demo videos to YouTube failed");
        }
    }
}
