namespace ShipLean.Application.Features.Mcp.Planning.Reads;

// One story or task of a plan; Dapper maps the columns onto the constructor by name (Tags is an Array: Npgsql reports text[] as one, and it is always a string[]).
internal sealed record PlanItemRow(
    Guid Id, Guid ParentId, string Key, string Title, string Description, string StateName, string StateCategory,
    string? Assignee, string Rank, Array Tags);
