using System.Threading.Channels;
using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Application.Features.Backups.Runs;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// What ProbeJob reports and how it behaves, shared by every scope: each start (its run and snapshot), each disposal (the
// worker's scope ends after the lease is released), an optional gate that holds the job until the test opens it, and
// Throw to make the job throw instead of recording success.
public sealed class JobProbe
{
    private static readonly TimeSpan Limit = TimeSpan.FromSeconds(30);

    private readonly Channel<(BackupRunRow Run, BackupConfig Snapshot)> _starts = Channel.CreateUnbounded<(BackupRunRow, BackupConfig)>();
    private readonly Channel<Guid> _ends = Channel.CreateUnbounded<Guid>();
    private int _startCount;

    public TaskCompletionSource? Gate { get; set; }

    public bool Throw { get; set; }

    public int StartCount => Volatile.Read(ref _startCount);

    public void Started(BackupRunRow run, BackupConfig snapshot)
    {
        Interlocked.Increment(ref _startCount);
        _starts.Writer.TryWrite((run, snapshot));
    }

    public void Ended(Guid runId) => _ends.Writer.TryWrite(runId);

    public Task<(BackupRunRow Run, BackupConfig Snapshot)> NextStartAsync() => ReadAsync(_starts, "a job to start");

    // The run whose job ended and whose lease the worker has released.
    public Task<Guid> NextEndAsync() => ReadAsync(_ends, "a job's lease to be released");

    private static async Task<T> ReadAsync<T>(Channel<T> channel, string what)
    {
        using var limit = new CancellationTokenSource(Limit);
        try
        {
            return await channel.Reader.ReadAsync(limit.Token);
        }
        catch (OperationCanceledException) when (limit.IsCancellationRequested)
        {
            throw new TimeoutException($"Timed out after {Limit.TotalSeconds:0} s waiting for {what}.");
        }
    }
}
