using ShipLean.Application.Features.Backups.S3;
using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Backups;

namespace ShipLean.WebApi.Tests.Backups.S3;

// BackupDestinationContract on S3Server: one bucket for the class, every test on its own keys.
[TestFixture]
public class S3BackupDestinationContractTests : BackupDestinationContract
{
    private S3BackupDestination _destination = null!;
    private S3BackupDestination _unreachable = null!;

    [OneTimeSetUp]
    public async Task CreateDestinations()
    {
        var config = await S3TestBucket.NewAsync();
        _destination = new S3BackupDestination(config);
        _unreachable = new S3BackupDestination(config with { SecretKey = "not-the-secret" });
    }

    [OneTimeTearDown]
    public void DisposeDestinations()
    {
        _destination.Dispose();
        _unreachable.Dispose();
    }

    protected override IBackupDestination Create() => _destination;

    protected override IBackupDestination CreateUnreachable() => _unreachable;
}
