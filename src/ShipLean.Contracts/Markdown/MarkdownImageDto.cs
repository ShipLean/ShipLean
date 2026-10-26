namespace ShipLean.Contracts.Markdown;

// An image the Markdown refers to, once per distinct Src. AttachmentId is set only when Status is Attached.
public sealed record MarkdownImageDto(string Src, MarkdownImageStatus Status, Guid? AttachmentId);
