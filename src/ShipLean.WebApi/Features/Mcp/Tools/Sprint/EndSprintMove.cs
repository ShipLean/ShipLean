namespace ShipLean.WebApi.Features.Mcp.Tools.Sprint;

// One item end_sprint will move: its revision at preview, and its destination sprint (null = the backlog).
public sealed record EndSprintMove(Guid Id, int Revision, Guid? TargetSprintId);
