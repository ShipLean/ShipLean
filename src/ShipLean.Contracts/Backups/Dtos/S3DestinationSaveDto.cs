namespace ShipLean.Contracts.Backups.Dtos;

// The S3 destination as PUT and the connection test send it. SecretKey null keeps the saved one; "" clears it, and then
// AccessKeyId must be null too.
public sealed record S3DestinationSaveDto(
    string Region,
    string? EndpointUrl,
    string Bucket,
    string Prefix,
    bool ForcePathStyle,
    string? AccessKeyId,
    string? SecretKey);
