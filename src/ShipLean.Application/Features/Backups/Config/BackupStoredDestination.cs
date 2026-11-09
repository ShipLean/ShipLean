using System.Text.Json;

namespace ShipLean.Application.Features.Backups.Config;

// backup_settings.destination: the S3 destination without its secret key, as jsonb.
public sealed record BackupStoredDestination(
    string Region,
    string? EndpointUrl,
    string Bucket,
    string Prefix,
    bool ForcePathStyle,
    string? AccessKeyId)
{
    public const string S3Provider = "s3";

    public string ToJson() => JsonSerializer.Serialize(this, JsonSerializerOptions.Web);

    public static BackupStoredDestination Parse(string json)
        => JsonSerializer.Deserialize<BackupStoredDestination>(json, JsonSerializerOptions.Web)
           ?? throw new InvalidOperationException("The saved backup destination is empty.");
}
