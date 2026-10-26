namespace ShipLean.Contracts.Markdown;

// WorkItemId, when given, is the item whose attachments the images are matched against (the caller must be a member).
public sealed record MarkdownRenderRequest(string Markdown, Guid? WorkItemId = null);
