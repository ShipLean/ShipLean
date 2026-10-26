using Agoda.IoC.Core;
using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Persisted;

namespace ShipLean.Application.Features.Links.OpenGraph;

// The preview image and favicon a card's ImageUrl and FaviconUrl point at, read from IBlobStore: the browser never loads the
// external address. Any member of the link's project may read them; a card with none, an archived link, or a blob stored under
// any type but the raster ones PreviewImageFormat sniffs (defence in depth: only those are ever written) is not_found.
[RegisterPerRequest]
public sealed class LinkCardImages(IProjectAccess access, LinkCardRows rows, IBlobStore blobs)
{
    public Task<BlobContent> OpenImageAsync(Guid linkId, CancellationToken cancellationToken)
        => OpenAsync(linkId, row => row.PreviewImageKey, "That link has no preview image.", cancellationToken);

    public Task<BlobContent> OpenFaviconAsync(Guid linkId, CancellationToken cancellationToken)
        => OpenAsync(linkId, row => row.PreviewFaviconKey, "That link has no favicon.", cancellationToken);

    private async Task<BlobContent> OpenAsync(Guid linkId, Func<LinkCardRow, string?> keyOf, string missing, CancellationToken cancellationToken)
    {
        await access.RequireAsync(ProjectItem.Link, linkId, ProjectRole.Member, cancellationToken);
        var key = await rows.FindAsync(linkId, cancellationToken) is { } row ? keyOf(row) : null;
        var content = key is null ? null : await blobs.GetAsync(key, cancellationToken);
        if (content is not null && !PreviewImageFormat.ContentTypes.Contains(content.ContentType))
        {
            await content.Content.DisposeAsync();
            content = null;
        }
        return content ?? throw ApiException.NotFound(missing);
    }
}
