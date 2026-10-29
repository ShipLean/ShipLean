using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace ShipLean.Application.Features.Holidays;

// The background holiday job: a full refresh on start, then one a day, and a refresh of a single country whenever one is
// tracked (HolidayRefreshQueue). A failure is logged by the refresher and the loop carries on. StopAsync cancels the work in
// flight and waits for it. Test hosts remove it by default (ShipLeanHost), so no test reaches the internet.
public sealed class HolidayRefreshService(HolidayRefresher refresher, HolidayRefreshQueue queue, TimeProvider clock, ILogger<HolidayRefreshService> logger) : IHostedService
{
    private static readonly TimeSpan Interval = TimeSpan.FromDays(1);

    private CancellationTokenSource? _stopping;
    private Task _loop = Task.CompletedTask;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stopping = new CancellationTokenSource();
        // Like the import runner: start without the starter's execution context.
        using (ExecutionContext.SuppressFlow())
        {
            _loop = Task.Run(() => RunAsync(_stopping.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _stopping, null) is not { } stopping)
        {
            return;
        }

        await stopping.CancelAsync();
        await Task.WhenAny(_loop, Task.Delay(Timeout.Infinite, cancellationToken));
        if (_loop.IsCompleted)
        {
            stopping.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken stopping)
    {
        var due = DateTimeOffset.MinValue;
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                if (clock.GetUtcNow() >= due)
                {
                    await refresher.RefreshAllAsync(stopping);
                    due = clock.GetUtcNow() + Interval;
                    continue;
                }

                using var wait = CancellationTokenSource.CreateLinkedTokenSource(stopping);
                wait.CancelAfter(due - clock.GetUtcNow());
                try
                {
                    await refresher.RefreshCountryAsync(await queue.ReadAsync(wait.Token), stopping);
                }
                catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
                {
                    // The day is up; the next turn refreshes everything.
                }
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                // The host is stopping.
            }
            catch (Exception failed)
            {
                logger.LogError(failed, "The holiday refresh loop failed; it will carry on");
                due = clock.GetUtcNow() + TimeSpan.FromMinutes(5);
            }
        }
    }
}
