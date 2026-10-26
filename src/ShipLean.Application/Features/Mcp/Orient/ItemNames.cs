namespace ShipLean.Application.Features.Mcp.Orient;

// The names a work item snapshot only holds ids for: its project key, state, assignee and sprint, the parents above it
// (nearest first) and the people behind the user ids of its history.
public sealed record ItemNames(
    string ProjectKey,
    string StateName,
    string? AssigneeName,
    string? SprintName,
    IReadOnlyList<ParentName> Parents,
    IReadOnlyDictionary<Guid, string> Users);
