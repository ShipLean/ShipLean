using ShipLean.Contracts.Attachments;

namespace ShipLean.Application.Markdown;

// The attachments of the work item a Markdown text belongs to, which its images are matched against.
public sealed record MarkdownImageContext(IReadOnlyList<AttachmentDto> Attachments);
