using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.Engine;

// Which place a run wrote to, so retention prunes only runs of the same destination (D8). A lowercase SHA-256 hex of
// the provider, the endpoint (or the region when there is none), the bucket and the prefix. The keys are left out, so
// rotating them keeps the id.
public static class BackupDestinationId
{
    public static string Of(BackupDestinationConfig config)
    {
        var s3 = config.S3 ?? throw new ArgumentException($"The {config.Provider} destination has no S3 settings.", nameof(config));
        string[] parts = [config.Provider, s3.EndpointUrl ?? s3.Region, s3.Bucket, s3.Prefix];
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(parts))));
    }
}
