namespace ShipLean.Contracts.Links;

// What adding a URL would show, before anything is saved: the card fields that exist without a stored link, plus a one-line
// explanation for the add-link form ("GitHub pull request · state stays live"). An unreachable host is not an error: it
// previews with PreviewStatus Unreachable.
public sealed record LinkPreviewDto(
    string Url,
    LinkKind Kind,
    LinkGroup Group,
    string Title,
    LinkTitleSource TitleSource,
    int? Number,
    string? Location,
    string Host,
    string? Author,
    LinkState? State,
    LinkStateSource StateSource,
    string? Description,
    string? ImageUrl,
    string? FaviconUrl,
    LinkPreviewStatus PreviewStatus,
    string Message);
