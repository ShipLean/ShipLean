namespace ShipLean.Contracts.Backups;

// An S3-compatible backup bucket. EndpointUrl is null for AWS itself; Prefix is prepended to every object key. Both
// keys null means the AWS default credential chain. ToString never prints the secret key, so logging it is safe.
public sealed record S3DestinationConfig(
    string Region,
    string? EndpointUrl,
    string Bucket,
    string Prefix,
    bool ForcePathStyle,
    string? AccessKeyId,
    string? SecretKey)
{
    public override string ToString() =>
        $"S3DestinationConfig {{ Region = {Region}, EndpointUrl = {EndpointUrl}, Bucket = {Bucket}, Prefix = {Prefix}, ForcePathStyle = {ForcePathStyle}, AccessKeyId = {AccessKeyId}, SecretKey = {(SecretKey is null ? "" : "***")} }}";
}
