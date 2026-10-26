namespace ShipLean.Application.Features.Taskboard;

// The task a move starts from.
internal sealed record TaskboardMoveRecord(Guid ProjectId, string Type, Guid? ParentId, string StateCategory);
