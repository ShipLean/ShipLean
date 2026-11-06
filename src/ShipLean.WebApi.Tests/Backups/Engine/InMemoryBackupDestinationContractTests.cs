using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Backups.Engine;

[TestFixture]
public class InMemoryBackupDestinationContractTests : BackupDestinationContract
{
    protected override IBackupDestination Create() => new InMemoryBackupDestination();

    protected override IBackupDestination CreateUnwritable()
    {
        var destination = new InMemoryBackupDestination();
        destination.Fail(BackupProbeResult.WriteStep);
        return destination;
    }
}
