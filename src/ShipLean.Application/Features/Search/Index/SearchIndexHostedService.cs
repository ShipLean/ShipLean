using System.Threading.Channels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ShipLean.Application.Features.Search.Index.Wake;
using ShipLean.Contracts.Events;
using ShipLean.Contracts.Search;

namespace ShipLean.Application.Features.Search.Index;

// The background indexer: a first pass after SearchOptions.FirstPassSeconds, then one every IntervalSeconds, and an early one
// whenever an item, comment, attachment, project or sprint event arrives. A wake first syncs the sources the events name (the
// union of a burst, once), then runs the pass. The first pass after startup is full (a full sync, so rows that predate the
// index get written; the writer only writes changed hashes); every later one is incremental.
// A failure is logged and the loop carries on. Sources whose sync failed are retried once on the next round, then dropped with
// an error (a full reindex recovers them), so one bad source cannot stall the passes. StopAsync cancels the in-flight pass and
// waits for it, because the container disposes the ONNX session right after.
public sealed class SearchIndexHostedService(
    IProjectEvents events,
    IServiceScopeFactory scopes,
    SearchWakeSources wake,
    SearchOptions options,
    ILogger<SearchIndexHostedService> logger) : IHostedService
{
    private readonly Channel<ProjectEvent> _wakes = Channel.CreateUnbounded<ProjectEvent>();
    private readonly List<ProjectEvent> _unmapped = [];
    private readonly HashSet<SearchSource> _unsynced = [];
    private CancellationTokenSource? _stopping;
    private IDisposable? _subscription;
    private Task _loop = Task.CompletedTask;
    private int _syncFailures;
    private bool _firstPassDone;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _stopping = new CancellationTokenSource();
        _subscription = events.Subscribe(null, projectEvent =>
        {
            if (SearchWakeSources.Wakes(projectEvent))
            {
                _wakes.Writer.TryWrite(projectEvent);
            }

            return Task.CompletedTask;
        });
        // Like the import runner: start without the starter's execution context.
        using (ExecutionContext.SuppressFlow())
        {
            _loop = Task.Run(() => RunAsync(_stopping.Token), CancellationToken.None);
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        _subscription?.Dispose();
        if (Interlocked.Exchange(ref _stopping, null) is not { } stopping)
        {
            return;
        }

        await stopping.CancelAsync();
        // The loop catches everything but cancellation, so awaiting it cannot fail; the host's stop token only cuts the wait short.
        await Task.WhenAny(_loop, Task.Delay(Timeout.Infinite, cancellationToken));
        if (_loop.IsCompleted)
        {
            // Else the loop still reads the token; the source is left to the finalizer.
            stopping.Dispose();
        }
    }

    private async Task RunAsync(CancellationToken stopping)
    {
        var delay = TimeSpan.FromSeconds(options.FirstPassSeconds);
        while (!stopping.IsCancellationRequested)
        {
            try
            {
                await WaitAsync(delay, stopping);
                delay = TimeSpan.FromSeconds(options.IntervalSeconds);
                await RoundAsync(stopping);
            }
            catch (OperationCanceledException) when (stopping.IsCancellationRequested)
            {
                // The host is stopping.
            }
            catch (Exception failed)
            {
                logger.LogError(failed, "The search indexing pass failed; it will run again");
            }
        }
    }

    // Returns when the delay is over or an event arrives, whichever is first.
    private async Task WaitAsync(TimeSpan delay, CancellationToken stopping)
    {
        using var either = CancellationTokenSource.CreateLinkedTokenSource(stopping);
        var timer = Task.Delay(delay, either.Token);
        var arrived = _wakes.Reader.WaitToReadAsync(either.Token).AsTask();
        await Task.WhenAny(timer, arrived);
        await either.CancelAsync();
        try
        {
            await Task.WhenAll(timer, arrived);
        }
        catch (OperationCanceledException) when (!stopping.IsCancellationRequested)
        {
            // The loser of the race, cancelled above.
        }
    }

    private async Task RoundAsync(CancellationToken stopping)
    {
        while (_wakes.Reader.TryRead(out var projectEvent))
        {
            _unmapped.Add(projectEvent);
        }

        if (_unmapped.Count > 0)
        {
            _unsynced.UnionWith(await wake.ForAsync(_unmapped, stopping));
            _unmapped.Clear();
        }

        await using var scope = scopes.CreateAsyncScope();
        if (_unsynced.Count > 0)
        {
            await SyncAsync(scope.ServiceProvider.GetRequiredService<ISearchDocWriter>(), stopping);
        }

        var full = !_firstPassDone;
        await scope.ServiceProvider.GetRequiredService<ISearchIndexer>().RunPassAsync(full, stopping);
        _firstPassDone = true;
    }

    // A failed sync must not stop the pass: the sources stay for one more try, then are dropped.
    private async Task SyncAsync(ISearchDocWriter writer, CancellationToken stopping)
    {
        try
        {
            await writer.SyncAsync([.. _unsynced], stopping);
            _unsynced.Clear();
            _syncFailures = 0;
        }
        catch (Exception failed) when (failed is not OperationCanceledException || !stopping.IsCancellationRequested)
        {
            if (++_syncFailures < 2)
            {
                logger.LogWarning(failed, "Syncing {Count} search sources failed; trying again next round", _unsynced.Count);
                return;
            }

            var kinds = string.Join(", ", _unsynced.Select(source => source.Kind).Distinct());
            logger.LogError(failed, "Syncing {Count} search sources (kinds: {Kinds}) failed {Failures} times in a row; dropping them, a full reindex recovers them",
                _unsynced.Count, kinds, _syncFailures);
            _unsynced.Clear();
            _syncFailures = 0;
        }
    }
}
