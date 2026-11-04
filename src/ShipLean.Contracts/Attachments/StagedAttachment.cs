namespace ShipLean.Contracts.Attachments;

// A file whose bytes are stored but whose row isn't written yet (IAttachmentStore.StageAsync). The id is fixed at staging so a
// caller can name it in a CommitStagedAsync hook. Not a wire shape.
public sealed record StagedAttachment(Guid Id, Guid WorkItemId, string Name, string ContentType, long SizeBytes, string StorageRef, bool Sanitized, string? SearchText, AttachmentSource? Source);
