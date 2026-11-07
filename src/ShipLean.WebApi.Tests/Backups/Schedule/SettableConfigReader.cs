using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.WebApi.Tests.Backups.Schedule;

// IBackupConfigReader over a config the test sets (null: nothing saved), counting reads.
public sealed class SettableConfigReader(BackupConfig? config) : IBackupConfigReader
{
    private int _reads;

    public BackupConfig? Config { get; set; } = config;

    public int Reads => Volatile.Read(ref _reads);

    public Task<BackupConfig?> ReadAsync(CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref _reads);
        return Task.FromResult(Config);
    }
}
