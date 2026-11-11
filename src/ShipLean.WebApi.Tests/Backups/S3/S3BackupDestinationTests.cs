using System.Net;
using System.Net.Sockets;
using Amazon.S3;
using Amazon.S3.Model;
using ShipLean.Application.Features.Backups.S3;
using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Storage;

namespace ShipLean.WebApi.Tests.Backups.S3;

[TestFixture]
public class S3BackupDestinationTests
{
    private const int ObjectCount = 1005;
    private const long PartSize = S3BackupDestination.MinimumMultipartThreshold;

    [Test]
    public async Task List_WhenOverThousandObjects_ShouldPage()
    {
        var config = await S3TestBucket.NewAsync();
        using var destination = new S3BackupDestination(config);
        await Parallel.ForEachAsync(
            Enumerable.Range(0, ObjectCount),
            new ParallelOptions { MaxDegreeOfParallelism = 8 },
            async (index, token) => await destination.PutAsync($"many/{index:D5}.slbk", new MemoryStream([1, 2]), token));

        var listed = new List<BackupObject>();
        await foreach (var item in destination.ListAsync("many/", CancellationToken.None))
        {
            listed.Add(item);
        }

        listed.Count.ShouldBe(ObjectCount);
        listed.Select(o => o.Key).Distinct().Count().ShouldBe(ObjectCount);
        listed.ShouldAllBe(o => o.Size == 2);
    }

    [Test]
    public async Task Probe_WhenBucketMissing_ShouldFailAtWrite()
    {
        var config = await S3TestBucket.NewAsync();
        using var destination = new S3BackupDestination(config with { Bucket = config.Bucket + "-gone" });

        var result = await destination.ProbeAsync(CancellationToken.None);

        result.Ok.ShouldBeFalse();
        result.Step.ShouldBe(BackupProbeResult.WriteStep);
        result.Message.ShouldContain(config.Bucket + "-gone");
    }

    [Test]
    public async Task Probe_WhenCredentialsWrong_ShouldFailWithoutSecret()
    {
        const string wrongSecret = "wrong-secret-key-value";
        var config = await S3TestBucket.NewAsync();
        using var destination = new S3BackupDestination(config with { SecretKey = wrongSecret });

        var result = await destination.ProbeAsync(CancellationToken.None);

        result.Ok.ShouldBeFalse();
        result.Step.ShouldBe(BackupProbeResult.WriteStep);
        result.Message.ShouldNotContain(wrongSecret);
        result.Message.ShouldNotContain(config.SecretKey!);
    }

    [Test]
    public async Task Put_WhenCredentialsWrong_ShouldNameBucketWithoutSecret()
    {
        const string wrongSecret = "wrong-secret-key-value";
        var config = await S3TestBucket.NewAsync();
        using var destination = new S3BackupDestination(config with { SecretKey = wrongSecret });

        var thrown = await Should.ThrowAsync<BackupDestinationException>(
            () => destination.PutAsync("a.slbk", new MemoryStream([1]), CancellationToken.None));

        thrown.Message.ShouldContain(config.Bucket);
        thrown.Message.ShouldNotContain(wrongSecret);
        thrown.Message.ShouldNotContain(config.SecretKey!);
    }

    [TestCase(true)]
    [TestCase(false)]
    public void Create_WhenEndpoint_ShouldUsePathStyleSetting(bool forcePathStyle)
    {
        var config = new S3DestinationConfig("eu-west-1", "https://s3.example.test", "b", "", forcePathStyle, "id", "secret");

        var settings = S3ClientSettings.From(config);

        settings.Config.ForcePathStyle.ShouldBe(forcePathStyle);
        new Uri(settings.Config.ServiceURL).ShouldBe(new Uri("https://s3.example.test"));
        settings.Config.AuthenticationRegion.ShouldBe("eu-west-1");
        settings.Credentials.ShouldNotBeNull();
    }

    [Test]
    public void Create_WhenNoKeys_ShouldUseDefaultChain()
    {
        var config = new S3DestinationConfig("eu-west-1", null, "b", "", false, null, null);

        var settings = S3ClientSettings.From(config);

        settings.Credentials.ShouldBeNull();
        settings.Config.RegionEndpoint.SystemName.ShouldBe("eu-west-1");
        settings.Config.ServiceURL.ShouldBeNull();
    }

    [Test]
    public async Task Put_WhenLarge_ShouldUseMultipart()
    {
        var config = await S3TestBucket.NewAsync();
        using var destination = new S3BackupDestination(config, PartSize);
        var content = new byte[(int)(PartSize * 2) + 1000];
        Random.Shared.NextBytes(content);

        await destination.PutAsync("big.slbk", new MemoryStream(content), CancellationToken.None);

        using var client = S3ClientSettings.From(config).CreateClient();
        var metadata = await client.GetObjectMetadataAsync(config.Bucket, "big.slbk");
        metadata.ETag.ShouldContain("-3");
        using var response = await client.GetObjectAsync(new GetObjectRequest { BucketName = config.Bucket, Key = "big.slbk" });
        using var read = new MemoryStream();
        await response.ResponseStream.CopyToAsync(read);
        read.ToArray().ShouldBe(content);
    }

    [Test]
    public async Task Put_WhenSmall_ShouldNotUseMultipart()
    {
        var config = await S3TestBucket.NewAsync();
        using var destination = new S3BackupDestination(config, PartSize);

        await destination.PutAsync("small.slbk", new MemoryStream([1, 2, 3]), CancellationToken.None);

        using var client = S3ClientSettings.From(config).CreateClient();
        (await client.GetObjectMetadataAsync(config.Bucket, "small.slbk")).ETag.ShouldNotContain("-");
    }

    [Test]
    public void Create_WhenOnlyAccessKey_ShouldThrow()
    {
        var config = new S3DestinationConfig("eu-west-1", null, "b", "", false, "id", null);

        var thrown = Should.Throw<BackupDestinationException>(() => S3ClientSettings.From(config));

        thrown.Message.ShouldBe("The s3 backup destination needs both an access key id and a secret key, or neither.");
    }

    [Test]
    public void Create_WhenOnlySecretKeyEmptyId_ShouldThrow()
    {
        var config = new S3DestinationConfig("eu-west-1", null, "b", "", false, "", "secret");

        Should.Throw<BackupDestinationException>(() => S3ClientSettings.From(config));
    }

    [Test]
    public void Create_WhenEndpointEmpty_ShouldUseAws()
    {
        var config = new S3DestinationConfig("eu-west-1", "", "b", "", false, null, null);

        var settings = S3ClientSettings.From(config);

        settings.Config.ServiceURL.ShouldBeNull();
        settings.Config.RegionEndpoint.SystemName.ShouldBe("eu-west-1");
    }

    [Test]
    public void ToString_WhenCredentials_ShouldLeaveThemOut()
    {
        var config = new S3DestinationConfig("eu-west-1", "https://s3.example.test", "b", "", true, "the-key-id", "the-secret-value");

        var text = S3ClientSettings.From(config).ToString();

        text.ShouldNotContain("the-key-id");
        text.ShouldNotContain("the-secret-value");
    }

    [Test]
    public async Task Exists_WhenBucketMissing_ShouldThrow()
    {
        var config = await S3TestBucket.NewAsync();
        using var destination = new S3BackupDestination(config with { Bucket = config.Bucket + "-gone" });

        var thrown = await Should.ThrowAsync<BackupDestinationException>(() => destination.ExistsAsync("a.slbk", CancellationToken.None));

        thrown.Message.ShouldContain(config.Bucket + "-gone");
    }

    [Test]
    public async Task Put_WhenEndpointTimesOut_ShouldThrowDestinationException()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var config = new S3DestinationConfig(
            "us-east-1", $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}", "b", "", true, "id", "secret");
        var settings = S3ClientSettings.From(config);
        settings.Config.Timeout = TimeSpan.FromMilliseconds(300);
        settings.Config.MaxErrorRetry = 0;
        using var destination = new S3BackupDestination(config, settings);

        var thrown = await Should.ThrowAsync<BackupDestinationException>(
            () => destination.PutAsync("a.slbk", new MemoryStream([1]), CancellationToken.None));

        thrown.Message.ShouldContain("'b'");
    }

    [Test]
    public async Task Probe_WhenEndpointTimesOut_ShouldReturnFailedStep()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var config = new S3DestinationConfig(
            "us-east-1", $"http://127.0.0.1:{((IPEndPoint)listener.LocalEndpoint).Port}", "b", "", true, "id", "secret");
        var settings = S3ClientSettings.From(config);
        settings.Config.Timeout = TimeSpan.FromMilliseconds(300);
        settings.Config.MaxErrorRetry = 0;
        using var destination = new S3BackupDestination(config, settings);

        var result = await destination.ProbeAsync(CancellationToken.None);

        result.Ok.ShouldBeFalse();
        result.Step.ShouldBe(BackupProbeResult.WriteStep);
    }
}
