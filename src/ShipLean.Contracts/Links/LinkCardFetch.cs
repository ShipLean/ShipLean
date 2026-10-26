namespace ShipLean.Contracts.Links;

// What one source read about an address. Sources store the preview image and favicon in IBlobStore themselves and return the keys. Site is the page's own site name (og:site_name); null when it has none.
public sealed record LinkCardFetch(
    LinkKind Kind,
    string? Title,
    int? Number,
    string? Location,
    string? Author,
    LinkState? State,
    string? Description,
    string? ImageKey,
    string? FaviconKey,
    LinkPreviewStatus PreviewStatus,
    string? Site = null);
