namespace ShipLean.Contracts.Backups;

// Makes the destination a config names. A provider it doesn't know throws BackupDestinationException.
public interface IBackupDestinationFactory
{
    IBackupDestination Create(BackupDestinationConfig config);
}
