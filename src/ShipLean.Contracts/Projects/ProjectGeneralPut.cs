namespace ShipLean.Contracts.Projects;

// The General section as saved: the key is chosen at creation and never changes.
public sealed record ProjectGeneralPut(string Name, string Description, IReadOnlyList<int> WorkingDays);
