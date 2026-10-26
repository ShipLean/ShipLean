namespace ShipLean.Application.Features.Mcp.Status;

// Type and StateCategory are the stored words ("task", "inProgress"). Unparented: no parent at all.
public sealed record SprintItem(
    string Key, string Title, string Type, string State, string StateCategory, decimal? Points, string? Assignee,
    bool Unparented, bool Blocked, IReadOnlyList<string> BlockedBy);
