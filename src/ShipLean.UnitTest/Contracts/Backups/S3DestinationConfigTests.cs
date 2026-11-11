using ShipLean.Contracts.Backups;

namespace ShipLean.UnitTest.Contracts.Backups;

[TestFixture]
public class S3DestinationConfigTests
{
    private static readonly S3DestinationConfig WithKeys =
        new("ap-southeast-1", "https://ceph.example.internal:7480", "shiplean-backups", "shiplean/", true, "AKEXAMPLE", "TOPSECRET");

    [Test]
    public void ToString_WhenSecretKeySet_ShouldHideIt()
    {
        var text = WithKeys.ToString();

        text.ShouldNotContain("TOPSECRET");
        text.ShouldContain("SecretKey = ***");
        text.ShouldContain("AccessKeyId = AKEXAMPLE");
        text.ShouldContain("Bucket = shiplean-backups");
        text.ShouldContain("EndpointUrl = https://ceph.example.internal:7480");
    }

    [Test]
    public void ToString_WhenInsideDestination_ShouldHideTheSecretKey()
    {
        new BackupDestinationConfig("s3", WithKeys).ToString().ShouldNotContain("TOPSECRET");
    }

    [Test]
    public void ToString_WhenNoSecretKey_ShouldNotClaimOne()
    {
        (WithKeys with { AccessKeyId = null, SecretKey = null }).ToString().ShouldNotContain("***");
    }
}
