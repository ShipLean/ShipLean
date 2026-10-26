using ShipLean.Application.Storage;

namespace ShipLean.UnitTest.Storage;

[TestFixture]
public class S3BlobConnectionTests
{
    [Test]
    public void Parse_WhenEverySettingIsGiven_ShouldReadThemAll()
    {
        var connection = S3BlobConnection.Parse(
            "bucket=shiplean;region=ap-southeast-1;serviceUrl=https://s3.example.test;accessKey=AK;secretKey=SK;forcePathStyle=true;prefix=prod/");

        connection.ShouldBe(new S3BlobConnection(
            "shiplean", "ap-southeast-1", new Uri("https://s3.example.test"), "AK", "SK", ForcePathStyle: true, "prod/"));
    }

    [Test]
    public void Parse_WhenOnlyBucketAndRegion_ShouldUseDefaults()
    {
        var connection = S3BlobConnection.Parse("bucket=shiplean;region=us-east-1");

        connection.ShouldBe(new S3BlobConnection("shiplean", "us-east-1", null, null, null, ForcePathStyle: false, ""));
    }

    [Test]
    public void Parse_WhenNamesDifferInCase_ShouldStillReadThem()
    {
        S3BlobConnection.Parse("Bucket=b;ServiceURL=http://minio:9000").ServiceUrl.ShouldBe(new Uri("http://minio:9000"));
    }

    [TestCase("region=us-east-1", "needs a bucket")]
    [TestCase("bucket=b", "needs a region or a serviceUrl")]
    [TestCase("bucket=b;region=r;accessKey=AK", "accessKey and secretKey together")]
    [TestCase("bucket=b;region=r;secretKey=SK", "accessKey and secretKey together")]
    [TestCase("bucket=b;serviceUrl=ftp://x", "not an http(s) URL")]
    [TestCase("bucket=b;serviceUrl=minio:9000", "not an http(s) URL")]
    [TestCase("bucket=b;region=r;forcePathStyle=yes", "not true or false")]
    [TestCase("bucket=b;region=r;endpoint=x", "unknown setting 'endpoint'")]
    [TestCase("bucket", "not a valid connection string")]
    public void Parse_WhenUnusable_ShouldSayWhy(string connectionString, string reason)
    {
        Should.Throw<FormatException>(() => S3BlobConnection.Parse(connectionString)).Message.ShouldContain(reason);
    }

    [Test]
    public void Parse_WhenUnusable_ShouldNotEchoTheSecretKey()
    {
        Should.Throw<FormatException>(() => S3BlobConnection.Parse("bucket=b;secretKey=TOPSECRET;accessKey=AK"))
            .Message.ShouldNotContain("TOPSECRET");
    }

    [Test]
    public void ToString_ShouldRedactTheSecretKeyOnly()
    {
        var text = S3BlobConnection.Parse("bucket=b;region=r;accessKey=AK;secretKey=TOPSECRET").ToString();

        text.ShouldNotContain("TOPSECRET");
        text.ShouldContain("secretKey=***");
        text.ShouldContain("accessKey=AK");
        text.ShouldContain("bucket=b");
    }
}
