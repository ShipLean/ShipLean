namespace ShipLean.Application.Features.Mcp.Status;

// Type and StateCategory are the stored words ("feature", "inProgress").
public sealed record ProgressHead(string ProjectKey, string Title, string Type, string State, string StateCategory);
