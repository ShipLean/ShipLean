using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Contracts.Events;

namespace ShipLean.Application.Features.IssueIntake.Progress;

// The hosted loop around ProgressReconciler (D13): a pass every 60 seconds, and one sooner when a WorkItemUpdated or LinksChanged
// event nudges it (nudges that arrive during a pass coalesce into one more pass). The events only wake the loop: what to post is
// always worked out from stored state, so a lost event or a restart loses nothing. Like ProposalJobService it starts without the
// starter's execution context, and a pass that fails is logged rather than allowed to stop the host.
public sealed class ProgressService(ProgressReconciler reconciler, IProjectEvents events, TimeProvider time, ILogger<ProgressService> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromSeconds(60);

    private readonly Channel<bool> _nudges = Channel.CreateBounded<bool>(new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

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
        using var subscription = events.Subscribe(null, projectEvent =>
        {
            if (projectEvent is WorkItemUpdated or LinksChanged)
            {
                _nudges.Writer.TryWrite(true);
            }

            return Task.CompletedTask;
        });
        using var timer = new PeriodicTimer(Every, time);
        Task<bool>? tick = null;
        Task<bool>? nudge = null;
        try
        {
            while (true)
            {
                await PassAsync(stoppingToken);
                tick ??= timer.WaitForNextTickAsync(stoppingToken).AsTask();
                nudge ??= _nudges.Reader.WaitToReadAsync(stoppingToken).AsTask();
                if (await Task.WhenAny(tick, nudge) == tick)
                {
                    if (!await tick)
                    {
                        return;
                    }

                    tick = null;
                }
                else
                {
                    await nudge;
                    _nudges.Reader.TryRead(out _);
                    nudge = null;
                }
            }
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
            await reconciler.RunPassAsync(stoppingToken);
        }
        catch (Exception failed) when (failed is not OperationCanceledException)
        {
            logger.LogError(failed, "The progress pass failed");
        }
    }
}
