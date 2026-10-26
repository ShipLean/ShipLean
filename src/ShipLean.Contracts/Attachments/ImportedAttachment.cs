namespace ShipLean.Contracts.Attachments;

// A file from another system; its bytes come as a stream at call time. The stored size is the number of bytes read.
public sealed record ImportedAttachment(
    string OriginalSystem,
    string OriginalSystemId,
    string Name,
    string ContentType,
    long SizeBytes,
    Guid? UploadedById,
    DateTimeOffset CreatedAt);
