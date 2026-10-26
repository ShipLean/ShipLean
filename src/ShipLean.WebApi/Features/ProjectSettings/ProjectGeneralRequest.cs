namespace ShipLean.WebApi.Features.ProjectSettings;

public sealed record ProjectGeneralRequest(string? Key, string Name, string Description, IReadOnlyList<int> WorkingDays);
