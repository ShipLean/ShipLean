namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoVideoDto(Guid AttachmentId, string ContentUrl, string Name, long SizeBytes, decimal DurationSeconds, string DurationLabel, string DeliveredLabel);
