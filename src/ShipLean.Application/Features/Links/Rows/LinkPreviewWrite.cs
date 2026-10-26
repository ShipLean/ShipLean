using ShipLean.Contracts.Links;

namespace ShipLean.Application.Features.Links.Rows;

// What a source read, as LinkCardRows stores it. Every field is written as given (null clears it).
public sealed record LinkPreviewWrite(
    string? Title,
    string? Description,
    string? Site,
    string? ImageKey,
    string? FaviconKey,
    LinkPreviewStatus Status,
    DateTimeOffset FetchedAt,
    int? Number,
    string? Location,
    string? Author);
