using System.Security.Cryptography;
using ShipLean.Application.Features.Links.PublicFetch;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.OpenGraph;

// The card for any other http/https page: the page's Open Graph tags, read through PublicFetcher (public addresses only, unless
// the host is allow-listed). The image and favicon are fetched the same way and stored in IBlobStore when their bytes are PNG,
// JPEG, GIF, WebP or ICO and whole (not cut at the cap); anything else is left out and the card still reads Ok. Blobs are keyed
// by their content's hash, so the same image on many cards is stored once.
public sealed class OpenGraphSource(PublicFetcher fetcher, IBlobStore blobs) : ILinkCardSource
{
    private const string PageAccept = "text/html,application/xhtml+xml;q=0.9,*/*;q=0.1";
    private const string ImageAccept = "image/png,image/jpeg,image/gif,image/webp,image/x-icon;q=0.9";

    public int Order => 100;

    public Task<bool> CanHandleAsync(Uri url, CancellationToken cancellationToken)
        => Task.FromResult(url.IsAbsoluteUri && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps));

    public async Task<LinkCardFetch> FetchAsync(Uri url, CancellationToken cancellationToken)
    {
        var page = await fetcher.FetchAsync(url, PageAccept, cancellationToken);
        if (page.Status != PublicFetchStatus.Ok)
        {
            var status = page.Status == PublicFetchStatus.Private ? LinkPreviewStatus.Private : LinkPreviewStatus.Unreachable;
            return new LinkCardFetch(LinkKind.Page, null, null, null, null, null, null, null, null, status);
        }

        var tags = IsHtml(page.ContentType) ? OpenGraphPage.Parse(page.Body, page.Address!) : new OpenGraphPage(null, null, null, null, null);
        var imageKey = await StoreAsync(tags.Image, cancellationToken);
        var faviconKey = await StoreAsync(tags.Icon, cancellationToken);
        return new LinkCardFetch(LinkKind.Page, tags.Title, null, null, null, null, tags.Description, imageKey, faviconKey, LinkPreviewStatus.Ok, tags.Site);
    }

    // A server that names no type gets the benefit of the doubt; one that names another type is not parsed.
    private static bool IsHtml(string? contentType)
        => contentType is null || string.Equals(contentType, "text/html", StringComparison.OrdinalIgnoreCase)
            || string.Equals(contentType, "application/xhtml+xml", StringComparison.OrdinalIgnoreCase);

    private async Task<string?> StoreAsync(Uri? address, CancellationToken cancellationToken)
    {
        if (address is null)
        {
            return null;
        }

        var image = await fetcher.FetchAsync(address, ImageAccept, cancellationToken);
        if (image.Status != PublicFetchStatus.Ok || image.Truncated || PreviewImageFormat.Sniff(image.Body) is not { } contentType)
        {
            return null;
        }

        var key = $"link-previews/{Convert.ToHexStringLower(SHA256.HashData(image.Body))}";
        using var content = new MemoryStream(image.Body, writable: false);
        await blobs.PutAsync(key, content, contentType, cancellationToken);
        return key;
    }
}
