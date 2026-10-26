namespace ShipLean.Application.Features.Links.PublicFetch;

// Limits of one public fetch: Timeout covers the whole fetch (every redirect and the body), MaxBytes is how much of the body is
// read, MaxRedirects how many redirects are followed.
public sealed record PublicFetchOptions(TimeSpan Timeout, int MaxBytes, int MaxRedirects)
{
    public static readonly PublicFetchOptions Default = new(TimeSpan.FromSeconds(5), 1024 * 1024, 3);

    // Connects only to public addresses (PinnedConnector with the registered IAddressCheck).
    public const string HttpClientName = "public-fetch";

    // Connects to any address: used only for a host an instance admin put on PreviewAllowHosts.
    public const string AllowedHostClientName = "public-fetch-allowed-host";
}
