using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using ShipLean.Contracts.Backups;

namespace ShipLean.TestSupport.Backups;

// A dictionary-backed IBackupDestination for tests. Fail(step) makes that step fail from then on, in calls and in the
// probe: write fails PutAsync, read fails ExistsAsync and ListAsync, delete fails DeleteAsync. Steps add up, so
// failing all three makes a destination nothing works on.
public sealed class InMemoryBackupDestination : IBackupDestination
{
    private static readonly string[] Steps = [BackupProbeResult.WriteStep, BackupProbeResult.ReadStep, BackupProbeResult.DeleteStep];

    private readonly ConcurrentDictionary<string, byte[]> _objects = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, bool> _failing = new(StringComparer.Ordinal);

    // BackupProbeResult.WriteStep, ReadStep or DeleteStep.
    public void Fail(string step)
    {
        if (!Steps.Contains(step, StringComparer.Ordinal))
        {
            throw new ArgumentOutOfRangeException(nameof(step), step, "Expected write, read or delete.");
        }

        _failing[step] = true;
    }

    // The bytes stored at key; KeyNotFoundException when it holds nothing.
    public byte[] Read(string key) => _objects[key];

    public async Task PutAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        Check(BackupProbeResult.WriteStep, key);
        using var buffer = new MemoryStream();
        await content.CopyToAsync(buffer, cancellationToken);
        _objects[key] = buffer.ToArray();
    }

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Check(BackupProbeResult.ReadStep, key);
        return Task.FromResult(_objects.ContainsKey(key));
    }

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        Check(BackupProbeResult.DeleteStep, key);
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<BackupObject> ListAsync(string prefix, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Check(BackupProbeResult.ReadStep, prefix);
        foreach (var (key, bytes) in _objects.ToArray().Where(o => o.Key.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(o => o.Key, StringComparer.Ordinal))
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Yield();
            yield return new BackupObject(key, bytes.Length);
        }
    }

    public async Task<BackupProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var key = $".shiplean-probe-{Guid.NewGuid():N}";
        foreach (var step in Steps)
        {
            try
            {
                if (step == BackupProbeResult.WriteStep)
                {
                    await PutAsync(key, new MemoryStream([1, 2, 3]), cancellationToken);
                }
                else if (step == BackupProbeResult.ReadStep)
                {
                    if (!await ExistsAsync(key, cancellationToken))
                    {
                        return new BackupProbeResult(false, step, "The test object was not there after writing it.");
                    }
                }
                else
                {
                    await DeleteAsync(key, cancellationToken);
                }
            }
            catch (BackupDestinationException e)
            {
                return new BackupProbeResult(false, step, e.Message);
            }
        }

        return new BackupProbeResult(true, null, "Wrote, read and deleted a test object.");
    }

    private void Check(string step, string key)
    {
        if (_failing.ContainsKey(step))
        {
            throw new BackupDestinationException($"The in-memory destination was told to fail at {step} ({key}).");
        }
    }
}
