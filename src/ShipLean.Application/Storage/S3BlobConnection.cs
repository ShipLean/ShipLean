using System.Data.Common;

namespace ShipLean.Application.Storage;

// SHIPLEAN_BLOB_S3_CONNECTION, parsed. Settings are name=value pairs separated by semicolons: bucket, region, serviceUrl,
// accessKey, secretKey, forcePathStyle and prefix (the README has an example). bucket is required, and so is region or
// serviceUrl (an S3-compatible service such as MinIO or R2 needs only its URL). accessKey and secretKey come as a pair;
// leave both out to use the AWS SDK's default credential chain (IAM role, environment, profile). prefix goes in front of
// every key as given.
public sealed record S3BlobConnection(
    string Bucket,
    string? Region,
    Uri? ServiceUrl,
    string? AccessKey,
    string? SecretKey,
    bool ForcePathStyle,
    string Prefix)
{
    private static readonly string[] Known = ["bucket", "region", "serviceUrl", "accessKey", "secretKey", "forcePathStyle", "prefix"];

    // Throws FormatException naming the problem (never echoing secretKey) when the string is unusable.
    public static S3BlobConnection Parse(string connectionString)
    {
        var builder = new DbConnectionStringBuilder();
        try
        {
            builder.ConnectionString = connectionString;
        }
        catch (ArgumentException)
        {
            throw new FormatException($"{BlobStoreServices.S3ConnectionKey} is not a valid connection string.");
        }

        var unknown = builder.Keys.Cast<string>().FirstOrDefault(key => !Known.Contains(key, StringComparer.OrdinalIgnoreCase));
        if (unknown is not null)
        {
            throw new FormatException($"{BlobStoreServices.S3ConnectionKey} has an unknown setting '{unknown}'.");
        }

        var bucket = Value(builder, "bucket") ?? throw new FormatException($"{BlobStoreServices.S3ConnectionKey} needs a bucket.");
        var region = Value(builder, "region");
        var serviceUrlText = Value(builder, "serviceUrl");
        Uri? serviceUrl = null;
        if (serviceUrlText is not null
            && (!Uri.TryCreate(serviceUrlText, UriKind.Absolute, out serviceUrl) || serviceUrl.Scheme is not ("http" or "https")))
        {
            throw new FormatException($"{BlobStoreServices.S3ConnectionKey} has a serviceUrl that is not an http(s) URL.");
        }

        if (region is null && serviceUrl is null)
        {
            throw new FormatException($"{BlobStoreServices.S3ConnectionKey} needs a region or a serviceUrl.");
        }

        var accessKey = Value(builder, "accessKey");
        var secretKey = Value(builder, "secretKey");
        if ((accessKey is null) != (secretKey is null))
        {
            throw new FormatException($"{BlobStoreServices.S3ConnectionKey} needs accessKey and secretKey together, or neither.");
        }

        var forcePathStyleText = Value(builder, "forcePathStyle");
        var forcePathStyle = false;
        if (forcePathStyleText is not null && !bool.TryParse(forcePathStyleText, out forcePathStyle))
        {
            throw new FormatException($"{BlobStoreServices.S3ConnectionKey} has a forcePathStyle that is not true or false.");
        }

        return new S3BlobConnection(bucket, region, serviceUrl, accessKey, secretKey, forcePathStyle, Value(builder, "prefix") ?? "");
    }

    // For logs: everything except the secret key.
    public override string ToString()
        => $"bucket={Bucket};region={Region};serviceUrl={ServiceUrl};accessKey={AccessKey};"
           + $"secretKey={(SecretKey is null ? "" : "***")};forcePathStyle={ForcePathStyle};prefix={Prefix}";

    private static string? Value(DbConnectionStringBuilder builder, string key)
        => builder.TryGetValue(key, out var value) && value is string text && !string.IsNullOrWhiteSpace(text) ? text.Trim() : null;
}
