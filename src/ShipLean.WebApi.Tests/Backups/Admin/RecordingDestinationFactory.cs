using System.Collections.Concurrent;
using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Backups.Admin;

// Hands out one in-memory destination for every s3 config and keeps the configs it was given, so a test can see which
// keys a connection test used.
public sealed class RecordingDestinationFactory : IBackupDestinationFactory
{
    private readonly ConcurrentQueue<BackupDestinationConfig> _configs = new();

    public InMemoryBackupDestination Destination { get; } = new();

    public IReadOnlyList<BackupDestinationConfig> Configs => [.. _configs];

    public IBackupDestination Create(BackupDestinationConfig config)
    {
        _configs.Enqueue(config);
        return Destination;
    }
}
