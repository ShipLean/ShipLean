namespace ShipLean.Contracts.Demos;

public sealed record DemoUploadedDto(string SprintName, Guid VideoAttachmentId, long VideoBytes, int Chapters, string OpenUrl);
