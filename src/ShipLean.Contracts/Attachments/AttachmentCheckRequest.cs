namespace ShipLean.Contracts.Attachments;

// The files about to be uploaded, asked about before any bytes are sent. POST api/work-items/{id}/attachments/check.
public sealed record AttachmentCheckRequest(IReadOnlyList<AttachmentCheckFile> Files);
