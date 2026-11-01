using Microsoft.Extensions.Hosting;

namespace ShipLean.Application.Features.Usage;

// TODO: batch what the queue holds and send it to the configured analytics sink; for now it drains and drops, so the queue never fills.
public sealed class UsageDispatcher(UsageQueue queue) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Read and drop: the discarded event is the stub's whole behaviour.
        while (await queue.Reader.WaitToReadAsync(stoppingToken))
        {
            while (queue.Reader.TryRead(out _))
            {
                // dropped
            }
        }
    }
}
