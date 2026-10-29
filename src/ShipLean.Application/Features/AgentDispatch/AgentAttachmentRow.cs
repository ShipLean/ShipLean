namespace ShipLean.Application.Features.AgentDispatch;

internal sealed record AgentAttachmentRow(Guid Id, string Name, string ContentType, long SizeBytes, string StorageRef);
