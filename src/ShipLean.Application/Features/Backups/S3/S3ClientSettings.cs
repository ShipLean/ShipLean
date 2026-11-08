using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.S3;

// How the SDK client for an S3 backup destination is set up: its config, and the keys it signs with. Credentials is null
// when the destination names no keys, which leaves the AWS default credential chain (IAM role, environment, profile) to
// the client. Checksums are calculated and validated only when S3 requires it, because the SDK's default checksums are
// rejected by some S3-compatible services.
public sealed record S3ClientSettings(AmazonS3Config Config, AWSCredentials? Credentials)
{
    public static S3ClientSettings From(S3DestinationConfig destination)
    {
        var config = new AmazonS3Config
        {
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            ForcePathStyle = destination.ForcePathStyle,
        };
        if (!string.IsNullOrWhiteSpace(destination.EndpointUrl))
        {
            config.ServiceURL = destination.EndpointUrl;
            config.AuthenticationRegion = destination.Region;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(destination.Region);
        }

        var hasKeyId = !string.IsNullOrEmpty(destination.AccessKeyId);
        var hasSecret = !string.IsNullOrEmpty(destination.SecretKey);
        if (hasKeyId != hasSecret)
        {
            throw new BackupDestinationException("The s3 backup destination needs both an access key id and a secret key, or neither.");
        }

        AWSCredentials? credentials = hasKeyId ? new BasicAWSCredentials(destination.AccessKeyId, destination.SecretKey) : null;
        return new S3ClientSettings(config, credentials);
    }

    // Leaves out the credentials, so the settings are safe to log.
    public override string ToString()
        => $"S3ClientSettings {{ ServiceURL = {Config.ServiceURL}, Region = {Config.RegionEndpoint?.SystemName ?? Config.AuthenticationRegion}, ForcePathStyle = {Config.ForcePathStyle}, Credentials = {(Credentials is null ? "default chain" : "***")} }}";

    public AmazonS3Client CreateClient() => Credentials is null ? new AmazonS3Client(Config) : new AmazonS3Client(Credentials, Config);
}
