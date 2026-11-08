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
        if (destination.EndpointUrl is not null)
        {
            config.ServiceURL = destination.EndpointUrl;
            config.AuthenticationRegion = destination.Region;
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(destination.Region);
        }

        AWSCredentials? credentials = destination.AccessKeyId is null || destination.SecretKey is null
            ? null
            : new BasicAWSCredentials(destination.AccessKeyId, destination.SecretKey);
        return new S3ClientSettings(config, credentials);
    }

    public AmazonS3Client CreateClient() => Credentials is null ? new AmazonS3Client(Config) : new AmazonS3Client(Credentials, Config);
}
