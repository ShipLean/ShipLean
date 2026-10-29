namespace ShipLean.Application.Features.AgentDispatch.Rendering;

// A file on the item. Text is the content of a Markdown attachment that fit the inline cap; null means the attachment is listed as a link only.
public sealed record AgentAttachmentInput(string Name, string ContentType, long SizeBytes, string Url, string? Text);
