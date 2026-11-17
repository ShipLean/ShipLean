namespace ShipLean.Contracts.Inspection;

// The tasks of one project (by key) whose tokens Inspection's Cost view reads, and the window the runs' links must fall in.
public sealed record TaskTokenQuery(string ProjectKey, IReadOnlyList<string> ItemKeys, DateTimeOffset From, DateTimeOffset To);
