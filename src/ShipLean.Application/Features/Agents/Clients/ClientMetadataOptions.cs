namespace ShipLean.Application.Features.Agents.Clients;

// Limits of a metadata-document fetch. A cached answer, good or refused, is kept for what its HTTP headers say, but never
// less than MinCache nor more than MaxCache; with no usable header it is kept for MinCache.
public sealed record ClientMetadataOptions(
    TimeSpan Timeout,
    int MaxBytes,
    TimeSpan MinCache,
    TimeSpan MaxCache,
    int MaxCachedUrls,
    int MaxConcurrentFetches)
{
    public static readonly ClientMetadataOptions Default = new(TimeSpan.FromSeconds(5), 64 * 1024, TimeSpan.FromMinutes(5), TimeSpan.FromHours(24), 1000, 8);

    public const string HttpClientName = "client-metadata";
}
