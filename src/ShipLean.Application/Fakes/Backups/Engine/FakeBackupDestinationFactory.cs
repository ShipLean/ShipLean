using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Fakes.Backups.Engine;

// Stands in for the S3 destination factory. Made without a destination (as its module registers it), an s3 config
// throws "not available yet"; a test host can register one made with a destination it holds (InMemoryBackupDestination)
// to get that destination for every s3 config. Any other provider throws, as the real factory does.
public sealed class FakeBackupDestinationFactory : IBackupDestinationFactory
{
    private readonly IBackupDestination? _destination;

    public FakeBackupDestinationFactory()
    {
    }

    public FakeBackupDestinationFactory(IBackupDestination destination) => _destination = destination;

    public IBackupDestination Create(BackupDestinationConfig config)
    {
        if (!string.Equals(config.Provider, "s3", StringComparison.Ordinal))
        {
            throw new BackupDestinationException($"There is no backup destination provider '{config.Provider}'.");
        }

        return _destination ?? throw new BackupDestinationException("S3 backup destinations are not available yet.");
    }
}
