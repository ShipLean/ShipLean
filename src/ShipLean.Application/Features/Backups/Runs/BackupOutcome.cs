namespace ShipLean.Application.Features.Backups.Runs;

// What a succeeded run wrote: the database object's key and size, and the attachment copies it made.
public sealed record BackupOutcome(string DatabaseObjectKey, long DatabaseBytes, int AttachmentsCopied, long AttachmentsBytes);
