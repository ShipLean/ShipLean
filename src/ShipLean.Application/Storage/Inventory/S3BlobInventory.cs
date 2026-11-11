using System.Runtime.CompilerServices;
using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using ShipLean.Application.Features.Backups.Engine;

namespace ShipLean.Application.Storage.Inventory;

// The keys of an S3BlobStore: ListObjectsV2 under the connection's prefix, one page at a time (continuation tokens), with
// the prefix stripped so the keys are the ones IBlobStore takes. Nothing is collected: each page's keys are yielded and dropped.
public sealed class S3BlobInventory : IBlobInventory, IDisposable
{
    private readonly S3BlobConnection _connection;
    private readonly AmazonS3Client _client;

    public S3BlobInventory(S3BlobConnection connection)
    {
        _connection = connection;
        _client = CreateClient(connection);
    }

    public async IAsyncEnumerable<string> ListKeysAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var request = new ListObjectsV2Request { BucketName = _connection.Bucket, Prefix = _connection.Prefix };
        ListObjectsV2Response page;
        do
        {
            page = await _client.ListObjectsV2Async(request, cancellationToken);
            foreach (var item in page.S3Objects ?? [])
            {
                // A "folder" placeholder (an empty object ending in "/", or the prefix itself) and a store's own temp file are not keys.
                var key = item.Key[_connection.Prefix.Length..];
                if (key.Length == 0 || key.EndsWith('/') || key.EndsWith(BlobKey.TempSuffix, StringComparison.Ordinal))
                {
                    continue;
                }

                yield return key;
            }

            request.ContinuationToken = page.NextContinuationToken;
        }
        while (page.IsTruncated == true);
    }

    public void Dispose() => _client.Dispose();

    // Built as S3BlobStore builds its client, so the same connection string reaches the same bucket.
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
}
