using ShipLean.Contracts.Refs;

namespace ShipLean.Application.Features.Mcp.Orient;

// What a search narrows by. Every clause is optional; Words are matched against title and description. StateName is a
// state's name in any item type of the project; State is one resolved state (the tool sets it when a type is given, and
// then StateName is null). Tag compares ignoring case.
public sealed record SearchFilter(
    Guid ProjectId,
    IReadOnlyList<string> Words,
    WorkItemTypeId? Type = null,
    string? StateName = null,
    Guid? Assignee = null,
    Guid? Sprint = null,
    string? Tag = null,
    Guid? Parent = null,
    Guid? State = null);
