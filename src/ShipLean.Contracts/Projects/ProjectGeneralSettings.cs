namespace ShipLean.Contracts.Projects;

public sealed record ProjectGeneralSettings(string Key, string Name, string Description, IReadOnlyList<int> WorkingDays);
