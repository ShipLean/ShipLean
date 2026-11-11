using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.S3;

// Makes S3 destinations (AWS, or an S3-compatible endpoint). Any other provider is a BackupDestinationException.
public sealed class S3BackupDestinationFactory : IBackupDestinationFactory
{
    public IBackupDestination Create(BackupDestinationConfig config)
    {
        if (!string.Equals(config.Provider, "s3", StringComparison.Ordinal))
        {
            throw new BackupDestinationException($"There is no backup destination provider '{config.Provider}'.");
        }

        var s3 = config.S3 ?? throw new BackupDestinationException("The s3 backup destination has no S3 settings.");
        if (string.IsNullOrWhiteSpace(s3.Bucket) || string.IsNullOrWhiteSpace(s3.Region))
        {
            throw new BackupDestinationException("The s3 backup destination needs a bucket and a region.");
        }

        return new S3BackupDestination(s3);
    }
}
