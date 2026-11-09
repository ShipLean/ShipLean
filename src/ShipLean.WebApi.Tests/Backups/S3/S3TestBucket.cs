using ShipLean.Contracts.Backups;
using ShipLean.TestSupport.Storage;

namespace ShipLean.WebApi.Tests.Backups.S3;

// A new bucket on S3Server, as the config a backup destination takes.
internal static class S3TestBucket
{
    public static async Task<S3DestinationConfig> NewAsync(string prefix = "")
    {
        var connection = await S3Server.NewBucketAsync();
        return new S3DestinationConfig(
            "us-east-1", connection.ServiceUrl!.ToString(), connection.Bucket, prefix, true, connection.AccessKey, connection.SecretKey);
    }
}
