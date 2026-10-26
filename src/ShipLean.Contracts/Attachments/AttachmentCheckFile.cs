namespace ShipLean.Contracts.Attachments;

public sealed record AttachmentCheckFile(string Name, long SizeBytes, string ContentType);
