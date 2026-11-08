using System.Net;
using System.Runtime.CompilerServices;
using System.Text;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using ShipLean.Contracts.Backups;

namespace ShipLean.Application.Features.Backups.S3;

// A backup destination in an S3 bucket (AWS or S3-compatible), under keys the caller builds; the config's Prefix is
// already in them, except for the probe object, which goes under it. The bucket must exist. A put of at most
// multipartThreshold bytes is one PutObject; a larger one is a multipart upload in parts of multipartThreshold bytes
// (S3 wants at least 5 MiB per part), aborted when a part fails. Every failure is a BackupDestinationException naming
// the bucket and the step, with the S3 error code and status but never the response body or a secret.
public sealed class S3BackupDestination : IBackupDestination, IDisposable
{
    public const long MinimumMultipartThreshold = 5 * 1024 * 1024;
    public const long DefaultMultipartThreshold = 64 * 1024 * 1024;

    private readonly S3DestinationConfig _config;
    private readonly long _multipartThreshold;
    private readonly AmazonS3Client _client;

    public S3BackupDestination(S3DestinationConfig config, long multipartThreshold = DefaultMultipartThreshold)
        : this(config, S3ClientSettings.From(config), multipartThreshold)
    {
    }

    // For a caller that tunes the client, such as its timeout and retries, beyond what the destination config sets.
    public S3BackupDestination(S3DestinationConfig config, S3ClientSettings settings, long multipartThreshold = DefaultMultipartThreshold)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(multipartThreshold, MinimumMultipartThreshold);
        _config = config;
        _multipartThreshold = multipartThreshold;
        _client = settings.CreateClient();
    }

    public Task PutAsync(string key, Stream content, CancellationToken cancellationToken)
        => Guard("write", () => PutCoreAsync(key, content, cancellationToken), cancellationToken);

    public Task<bool> ExistsAsync(string key, CancellationToken cancellationToken)
        => Guard("read", async () =>
        {
            try
            {
                await _client.GetObjectMetadataAsync(_config.Bucket, key, cancellationToken);
                return true;
            }
            catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                // A HEAD answer has no body, so a missing key and a missing bucket look the same: ask about the bucket.
                if (!await AmazonS3Util.DoesS3BucketExistV2Async(_client, _config.Bucket))
                {
                    throw new BackupDestinationException($"S3 read failed: the bucket '{_config.Bucket}' does not exist.", e);
                }

                return false;
            }
        }, cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
        => Guard("delete", () => _client.DeleteObjectAsync(_config.Bucket, key, cancellationToken), cancellationToken);

    public async IAsyncEnumerable<BackupObject> ListAsync(string prefix, [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        string? token = null;
        do
        {
            var page = await Guard("list", () => _client.ListObjectsV2Async(
                new ListObjectsV2Request { BucketName = _config.Bucket, Prefix = prefix, ContinuationToken = token }, cancellationToken),
                cancellationToken);
            foreach (var item in page.S3Objects ?? [])
            {
                yield return new BackupObject(item.Key, item.Size ?? 0);
            }

            token = page.IsTruncated == true ? page.NextContinuationToken : null;
        }
        while (token is not null);
    }

    public async Task<BackupProbeResult> ProbeAsync(CancellationToken cancellationToken)
    {
        var key = $"{_config.Prefix}.shiplean-probe-{Guid.NewGuid():N}";
        var payload = Encoding.UTF8.GetBytes(key);

        var step = BackupProbeResult.WriteStep;
        try
        {
            await _client.PutObjectAsync(
                new PutObjectRequest { BucketName = _config.Bucket, Key = key, InputStream = new MemoryStream(payload) }, cancellationToken);

            step = BackupProbeResult.ReadStep;
            using (var response = await _client.GetObjectAsync(_config.Bucket, key, cancellationToken))
            {
                using var read = new MemoryStream();
                await response.ResponseStream.CopyToAsync(read, cancellationToken);
                if (!read.ToArray().AsSpan().SequenceEqual(payload))
                {
                    await DeleteQuietlyAsync(key);
                    return new BackupProbeResult(false, step, $"S3 read on bucket '{_config.Bucket}' returned different bytes than were written.");
                }
            }

            step = BackupProbeResult.DeleteStep;
            await _client.DeleteObjectAsync(_config.Bucket, key, cancellationToken);
            return new BackupProbeResult(true, null, $"Wrote, read and deleted a test object in bucket '{_config.Bucket}'.");
        }
        catch (Exception e) when (IsDestinationFailure(e, cancellationToken))
        {
            if (step == BackupProbeResult.ReadStep)
            {
                await DeleteQuietlyAsync(key);
            }

            return new BackupProbeResult(false, step, Describe(step, e));
        }
    }

    public void Dispose() => _client.Dispose();

    private async Task PutCoreAsync(string key, Stream content, CancellationToken cancellationToken)
    {
        var remaining = content.Length - content.Position;
        if (remaining <= _multipartThreshold)
        {
            await _client.PutObjectAsync(
                new PutObjectRequest { BucketName = _config.Bucket, Key = key, InputStream = content, AutoCloseStream = false }, cancellationToken);
            return;
        }

        var upload = await _client.InitiateMultipartUploadAsync(
            new InitiateMultipartUploadRequest { BucketName = _config.Bucket, Key = key }, cancellationToken);
        try
        {
            var parts = new List<PartETag>();
            var number = 0;
            while (remaining > 0)
            {
                number++;
                var size = Math.Min(_multipartThreshold, remaining);
                var part = await _client.UploadPartAsync(
                    new UploadPartRequest
                    {
                        BucketName = _config.Bucket,
                        Key = key,
                        UploadId = upload.UploadId,
                        PartNumber = number,
                        PartSize = size,
                        InputStream = content,
                    },
                    cancellationToken);
                parts.Add(new PartETag(number, part.ETag));
                remaining -= size;
            }

            await _client.CompleteMultipartUploadAsync(
                new CompleteMultipartUploadRequest { BucketName = _config.Bucket, Key = key, UploadId = upload.UploadId, PartETags = parts },
                cancellationToken);
        }
        catch
        {
            await AbortQuietlyAsync(key, upload.UploadId);
            throw;
        }
    }

    // Best effort, for the probe object after a failed read: the failure being reported is the read's.
    private async Task DeleteQuietlyAsync(string key)
    {
        try
        {
            await _client.DeleteObjectAsync(_config.Bucket, key);
        }
        catch (Exception e) when (e is AmazonServiceException or AmazonClientException or HttpRequestException or IOException or TimeoutException or OperationCanceledException)
        {
            // The probe object is left for the bucket's owner; the read failure is reported.
        }
    }

    // What the SDK throws for a failed call. A timeout surfaces as a TimeoutException or as a cancellation the caller did not ask for.
    private static bool IsDestinationFailure(Exception e, CancellationToken cancellationToken)
        => e is AmazonServiceException or AmazonClientException or HttpRequestException or IOException or TimeoutException
            || (e is OperationCanceledException && !cancellationToken.IsCancellationRequested);

    private async Task AbortQuietlyAsync(string key, string uploadId)
    {
        try
        {
            await _client.AbortMultipartUploadAsync(_config.Bucket, key, uploadId);
        }
        catch (Exception e) when (e is AmazonServiceException or AmazonClientException or HttpRequestException)
        {
            // The upload's own failure is the one to report; an unaborted upload is cleaned up by the bucket's lifecycle rules.
        }
    }

    private async Task<T> Guard<T>(string step, Func<Task<T>> call, CancellationToken cancellationToken)
    {
        try
        {
            return await call();
        }
        catch (Exception e) when (IsDestinationFailure(e, cancellationToken))
        {
            throw new BackupDestinationException(Describe(step, e), e);
        }
    }

    private Task Guard(string step, Func<Task> call, CancellationToken cancellationToken) => Guard<object?>(step, async () =>
    {
        await call();
        return null;
    }, cancellationToken);

    private string Describe(string step, Exception e)
    {
        var detail = e switch
        {
            AmazonServiceException s => $"{(string.IsNullOrEmpty(s.ErrorCode) ? "error" : s.ErrorCode)}, HTTP {(int)s.StatusCode}",
            _ => e.GetType().Name,
        };
        return $"S3 {step} on bucket '{_config.Bucket}' failed ({detail}).";
    }
}
