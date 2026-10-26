using ShipLean.Contracts.Markdown;

namespace ShipLean.Contracts.Attachments;

// A Markdown attachment read for the reader page. GET api/attachments/{id}/markdown. Siblings are the item's other Markdown files.
public sealed record AttachmentMarkdownDto(
    AttachmentDto Attachment,
    MarkdownWorkItemDto WorkItem,
    string Markdown,
    string Html,
    IReadOnlyList<MarkdownOutlineEntryDto> Outline,
    IReadOnlyList<MarkdownImageDto> Images,
    IReadOnlyList<AttachmentDto> Siblings);
