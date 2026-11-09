namespace ShipLean.Contracts.Backups.Dtos;

// The saved S3 destination as GET returns it. The secret key never comes back: SecretKeyLast4 is null when none is saved.
// EndpointUrl is null for AWS itself; both keys null means the AWS default credential chain.
public sealed record S3DestinationDto(
    string Region,
    string? EndpointUrl,
    string Bucket,
    string Prefix,
    bool ForcePathStyle,
    string? AccessKeyId,
    string? SecretKeyLast4);
