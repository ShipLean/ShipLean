using ShipLean.Application.Features.Backups.Engine;
using ShipLean.Contracts.Backups;

namespace ShipLean.UnitTest.Backups;

[TestFixture]
public class BackupConfigTests
{
    [Test]
    public void ToString_WhenSecretsSet_ShouldHideThem()
    {
        var destination = new BackupDestinationConfig(
            "s3", new S3DestinationConfig("us-east-1", null, "shiplean-backups", "shiplean/", false, "AKEXAMPLE", "TOPSECRET"));
        var config = new BackupConfig(
            true, destination, "d1", "0 2 * * *", 14, true, false, "correct horse battery staple", 2, new DateTimeOffset(2026, 10, 24, 2, 0, 0, TimeSpan.Zero));

        var text = config.ToString();

        text.ShouldNotContain("TOPSECRET");
        text.ShouldNotContain("correct horse");
        text.ShouldContain("Passphrase = ***");
        text.ShouldContain("PassphraseGeneration = 2");
        text.ShouldContain("Bucket = shiplean-backups");
    }
}
