using ShipLean.Contracts;

namespace ShipLean.Application.Features.Mcp.Orient;

// A project the current user is in, with their role and the sprint running today (null when none is).
public sealed record ProjectSummary(Guid Id, string Key, string Name, ProjectRole Role, SprintSummary? CurrentSprint);
