namespace ShipLean.Application.Features.Links.PublicFetch;

// What a public fetch read. Body is at most MaxBytes; Truncated says the server had more. Address is where the body came from
// (after redirects), for resolving relative links in it.
public sealed record PublicFetchResult(PublicFetchStatus Status, Uri? Address, byte[] Body, string? ContentType, bool Truncated)
{
    public static PublicFetchResult Unreachable { get; } = new(PublicFetchStatus.Unreachable, null, [], null, false);

    public static PublicFetchResult Private { get; } = new(PublicFetchStatus.Private, null, [], null, false);
}
