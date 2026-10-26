namespace ShipLean.Contracts.Attachments;

// Message is the sentence to show beside the file ("" when the outcome needs none).
public sealed record AttachmentCheckOutcomeDto(string Name, AttachmentOutcome Outcome, string Message);
