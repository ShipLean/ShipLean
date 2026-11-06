using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.Application.Fakes.Backups.Engine;

// Stands in for the database-backed reader: always returns the config it was made with. Its module makes it with
// null (nothing saved); a test host can register one with a config.
public sealed class FixedBackupConfigReader(BackupConfig? config) : IBackupConfigReader
{
    public Task<BackupConfig?> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(config);
}
