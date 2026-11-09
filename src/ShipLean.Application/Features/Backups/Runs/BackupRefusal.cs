namespace ShipLean.Application.Features.Backups.Runs;

// Why TryStartAsync started nothing: another run holds the lease (Busy), or this scheduled occurrence already has a row
// on some instance (OccurrenceTaken).
public enum BackupRefusal
{
    Busy,
    OccurrenceTaken,
}
