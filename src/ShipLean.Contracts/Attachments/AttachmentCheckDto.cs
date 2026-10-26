namespace ShipLean.Contracts.Attachments;

// One outcome per requested file, in request order.
public sealed record AttachmentCheckDto(IReadOnlyList<AttachmentCheckOutcomeDto> Files);
