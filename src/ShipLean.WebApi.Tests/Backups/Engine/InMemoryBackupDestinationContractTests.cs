using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Backups.Engine;

[TestFixture]
public class InMemoryBackupDestinationContractTests : BackupDestinationContract
{
    protected override IBackupDestination Create() => new InMemoryBackupDestination();

    protected override IBackupDestination CreateUnreachable()
    {
        var destination = new InMemoryBackupDestination();
        destination.Fail(BackupProbeResult.WriteStep);
        destination.Fail(BackupProbeResult.ReadStep);
        destination.Fail(BackupProbeResult.DeleteStep);
        return destination;
    }
}
