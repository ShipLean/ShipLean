using ShipLean.Contracts.Refs;

namespace ShipLean.Contracts.Links;

// One URL link as a card. Nothing is omitted: absent values are null. ImageUrl and FaviconUrl are always "/api/link-cards/{id}/image"
// and ".../favicon" (ShipLean serves the bytes it stored) or null, never an external address. StateAsOfLabel is formatted by the server.
public sealed record LinkCardDto(
    Guid Id,
    string Url,
    LinkKind Kind,
    LinkGroup Group,
    string Title,
    LinkTitleSource TitleSource,
    string? Note,
    int? Number,
    string? Location,
    string Host,
    string? Author,
    LinkState? State,
    LinkStateSource StateSource,
    DateTimeOffset? StateAsOf,
    string? StateAsOfLabel,
    string? ReportedBy,
    string? Description,
    string? ImageUrl,
    string? FaviconUrl,
    LinkPreviewStatus PreviewStatus,
    bool RefreshDue);
