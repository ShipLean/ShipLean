using Agoda.IoC.Core;
using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.Cards;

// Reads an address through the first ILinkCardSource (lowest Order) that handles it, and maps what it read onto a card's
// stored preview. The card service and the refresh both read through it, so they pick the same source.
[RegisterPerRequest]
public sealed class LinkSourceReader(IEnumerable<ILinkCardSource> sources)
{
    // Null when no source handles the address.
    public async Task<LinkCardFetch?> ReadAsync(Uri uri, CancellationToken cancellationToken)
    {
        foreach (var source in sources.OrderBy(s => s.Order))
        {
            if (await source.CanHandleAsync(uri, cancellationToken))
            {
                return await source.FetchAsync(uri, cancellationToken);
            }
        }

        return null;
    }

    public async Task<bool> AnyHandlesAsync(Uri uri, CancellationToken cancellationToken)
    {
        foreach (var source in sources.OrderBy(s => s.Order))
        {
            if (await source.CanHandleAsync(uri, cancellationToken))
            {
                return true;
            }
        }

        return false;
    }

    public static LinkKind KindOf(LinkCardFetch fetch, LinkAddress address) => fetch.Kind != LinkKind.Page ? fetch.Kind : address.Kind;

    // What the card stores of a read: a source's title is the preview title, never the typed one.
    public static LinkPreviewWrite PreviewOf(LinkCardFetch fetch, LinkAddress address, DateTimeOffset fetchedAt)
        => new(fetch.Title, fetch.Description, fetch.Site ?? address.Host, fetch.ImageKey, fetch.FaviconKey, fetch.PreviewStatus, fetchedAt, fetch.Number, fetch.Location, fetch.Author);
}
