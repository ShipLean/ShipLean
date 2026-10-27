namespace ShipLean.Contracts.Attachments;

// What the upload endpoint answers. Replaced: a live file of the same name was replaced.
public sealed record UploadedAttachmentDto(Guid Id, string Name, long SizeBytes, bool Replaced);
