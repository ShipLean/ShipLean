namespace ShipLean.Contracts.Projects;

public sealed record ProjectSummaryDto(Guid Id, string Key, string Name, ProjectRole Role, int MemberCount);
