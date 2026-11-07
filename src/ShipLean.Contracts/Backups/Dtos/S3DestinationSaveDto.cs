namespace ShipLean.Contracts.Backups.Dtos;

// The S3 destination as PUT and the connection test send it. SecretKey null keeps the saved one; "" clears it, and then
// AccessKeyId must be null too. ToString never prints the secret key (MVC logs action arguments at Trace).
public sealed record S3DestinationSaveDto(
    string Region,
    string? EndpointUrl,
    string Bucket,
    string Prefix,
    bool ForcePathStyle,
    string? AccessKeyId,
    string? SecretKey)
{
    public override string ToString() =>
        $"S3DestinationSaveDto {{ Region = {Region}, EndpointUrl = {EndpointUrl}, Bucket = {Bucket}, Prefix = {Prefix}, ForcePathStyle = {ForcePathStyle}, AccessKeyId = {AccessKeyId}, SecretKey = {(SecretKey is null ? "" : "***")} }}";
}
