namespace ShipLean.Contracts.WorkItems;

// Where an imported item came from: the system ("jira"), its id for the item (the Jira issue key) and a link to it.
public sealed record OriginalSystemDto(string System, string Id, string? Url);
