using System.Net;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using ShipLean.Contracts.Blobs;

namespace ShipLean.Application.Storage;

// Blobs as objects in an S3 bucket (AWS or any S3-compatible service), under the connection's prefix. The content type
// is the object's Content-Type. The bucket must already exist. S3 replaces an object whole, so a put is all or nothing
// as long as the upload starts only once the content is complete: PutObject needs a known length and the upload stream
// has none, so the content is first copied to a temporary file (deleted when closed; attachments are capped at 25 MiB
// by default). A content stream that fails therefore fails before S3 is touched. Checksums are calculated and validated
// only when S3 requires it, because the SDK's default checksums are rejected by some S3-compatible services.
public sealed class S3BlobStore : IBlobStore, IDisposable
{
    private readonly S3BlobConnection _connection;
    private readonly AmazonS3Client _client;

    public S3BlobStore(S3BlobConnection connection)
    {
        _connection = connection;
        _client = CreateClient(connection);
    }

    public async Task<long> PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        var objectKey = ObjectKey(key);
        await using var buffer = new FileStream(
            Path.Combine(Path.GetTempPath(), $"shiplean-s3-{Guid.NewGuid():N}.tmp"), FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
            FileOptions.DeleteOnClose | FileOptions.Asynchronous);
        await content.CopyToAsync(buffer, cancellationToken);
        var length = buffer.Length;
        buffer.Position = 0;

        await WithBucketCheck(() => _client.PutObjectAsync(
            new PutObjectRequest { BucketName = _connection.Bucket, Key = objectKey, InputStream = buffer, ContentType = contentType },
            cancellationToken));
        return length;
    }

    public async Task<BlobContent?> GetAsync(string key, CancellationToken cancellationToken)
    {
        var objectKey = ObjectKey(key);
        try
        {
            var response = await WithBucketCheck(() => _client.GetObjectAsync(_connection.Bucket, objectKey, cancellationToken));
            return new BlobContent(new S3ObjectBody(response), response.Headers.ContentType);
        }
        catch (AmazonS3Exception e) when (e.ErrorCode == "NoSuchKey")
        {
            return null;
        }
    }

    public async Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
    {
        var objectKey = ObjectKey(key);
        try
        {
            await _client.GetObjectMetadataAsync(_connection.Bucket, objectKey, cancellationToken);
            return true;
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // A HEAD answer has no body, so a missing key and a missing bucket look the same: ask about the bucket.
            if (!await AmazonS3Util.DoesS3BucketExistV2Async(_client, _connection.Bucket))
            {
                throw MissingBucket(e);
            }

            return false;
        }
    }

    public void Dispose() => _client.Dispose();

    private static AmazonS3Client CreateClient(S3BlobConnection connection)
    {
        var config = new AmazonS3Config
        {
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            ForcePathStyle = connection.ForcePathStyle,
        };
        if (connection.ServiceUrl is not null)
        {
            config.ServiceURL = connection.ServiceUrl.ToString();
            config.AuthenticationRegion = connection.Region ?? "us-east-1";
        }
        else
        {
            config.RegionEndpoint = RegionEndpoint.GetBySystemName(connection.Region);
        }

        return connection.AccessKey is null
            ? new AmazonS3Client(config)
            : new AmazonS3Client(new BasicAWSCredentials(connection.AccessKey, connection.SecretKey), config);
    }

    private string ObjectKey(string key)
    {
        BlobKey.Check(key);
        return _connection.Prefix + key;
    }

    private async Task<T> WithBucketCheck<T>(Func<Task<T>> call)
    {
        try
        {
            return await call();
        }
        catch (AmazonS3Exception e) when (e.ErrorCode == "NoSuchBucket")
        {
            throw MissingBucket(e);
        }
    }

    private InvalidOperationException MissingBucket(Exception inner)
        => new($"The S3 bucket '{_connection.Bucket}' does not exist. Create it before using {BlobStoreServices.S3ConnectionKey}.", inner);
}
