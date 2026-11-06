using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Backups;

namespace ShipLean.UnitTest.Backups.Engine;

[TestFixture]
public class BackupDestinationIdTests
{
    private static readonly S3DestinationConfig Bucket = new("eu-west-1", "https://ceph.example.test", "backups", "shiplean/", true, "AKIA1", "secret-1");

    [Test]
    public void DestinationId_WhenKeysChange_ShouldStaySame()
    {
        var before = BackupDestinationId.Of(new BackupDestinationConfig("s3", Bucket));

        var after = BackupDestinationId.Of(new BackupDestinationConfig("s3", Bucket with { AccessKeyId = "AKIA2", SecretKey = "secret-2" }));

        after.ShouldBe(before);
        after.ShouldMatch("^[0-9a-f]{64}$");
    }

    [Test]
    public void DestinationId_WhenBucketChanges_ShouldChange()
    {
        var before = BackupDestinationId.Of(new BackupDestinationConfig("s3", Bucket));

        var after = BackupDestinationId.Of(new BackupDestinationConfig("s3", Bucket with { Bucket = "backups-2" }));

        after.ShouldNotBe(before);
    }
}
