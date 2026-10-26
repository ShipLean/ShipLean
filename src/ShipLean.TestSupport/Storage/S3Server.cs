using Amazon.Runtime;
using Amazon.S3;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using ShipLean.Application.Storage;

namespace ShipLean.TestSupport.Storage;

// The S3-compatible server the blob store tests share (never real AWS). SHIPLEAN_TEST_S3 (a connection string without a
// bucket, e.g. "serviceUrl=http://127.0.0.1:9000;accessKey=...;secretKey=...;forcePathStyle=true") wins; otherwise one
// RustFS container (ghcr.io/rustfs/rustfs, pinned) is started and reused across runs. It has to replace objects
// atomically, as S3 does: the blob store contract reads a key while it is overwritten, and a server whose GET can mix
// one version's headers with another's bytes fails it (versitygw's posix backend does; see docs/testing.md). Each caller
// gets a bucket of its own (NewBucketAsync), so test classes never share keys.
public static class S3Server
{
    private const string Image = "ghcr.io/rustfs/rustfs:1.0.3";
    private const string AccessKey = "shiplean";
    private const string SecretKey = "shiplean-secret";
    private const int Port = 9000;

    private static readonly Lazy<Task<string>> Server = new(StartAsync);

    // A new, empty bucket on the server, as a connection (with the given key prefix) for S3BlobStore.
    public static async Task<S3BlobConnection> NewBucketAsync(string prefix = "")
    {
        var bucket = $"shiplean-test-{Guid.NewGuid():N}"[..40];
        var server = await Server.Value;
        var connection = S3BlobConnection.Parse($"{server};bucket={bucket};prefix={prefix}");
        using var client = CreateClient(connection);
        await client.PutBucketAsync(bucket);
        return connection;
    }

    // The SHIPLEAN_BLOB_S3_CONNECTION value for a connection from NewBucketAsync (ToString leaves the secret key out).
    public static string ToSetting(S3BlobConnection connection)
        => $"serviceUrl={connection.ServiceUrl};accessKey={connection.AccessKey};secretKey={connection.SecretKey};"
           + $"forcePathStyle={connection.ForcePathStyle};bucket={connection.Bucket};prefix={connection.Prefix}";

    // An SDK client of the test's own, for looking at what the store left in the bucket.
    public static AmazonS3Client CreateClient(S3BlobConnection connection)
        => new(
            new BasicAWSCredentials(connection.AccessKey, connection.SecretKey),
            new AmazonS3Config { ServiceURL = connection.ServiceUrl!.ToString(), ForcePathStyle = true, AuthenticationRegion = "us-east-1" });

    private static async Task<string> StartAsync()
    {
        var configured = Environment.GetEnvironmentVariable("SHIPLEAN_TEST_S3");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured;
        }

        var container = new ContainerBuilder(Image)
            .WithEnvironment("RUSTFS_ACCESS_KEY", AccessKey)
            .WithEnvironment("RUSTFS_SECRET_KEY", SecretKey)
            .WithEnvironment("RUSTFS_VOLUMES", "/data")
            // World-writable: the image runs as an unprivileged user, and a plain tmpfs mount is root-only.
            .WithCreateParameterModifier(parameters => (parameters.HostConfig ??= new()).Tmpfs = new Dictionary<string, string> { ["/data"] = "rw,mode=1777" })
            .WithPortBinding(Port, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(Port).ForPath("/health/ready")))
            .WithReuse(true)
            .WithLabel("shiplean-tests", "rustfs-1.0.3")
            .Build();
        await container.StartAsync();
        return $"serviceUrl=http://{container.Hostname}:{container.GetMappedPublicPort(Port)};accessKey={AccessKey};secretKey={SecretKey};forcePathStyle=true";
    }
}
