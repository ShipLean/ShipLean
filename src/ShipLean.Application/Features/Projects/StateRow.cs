namespace ShipLean.Application.Features.Projects;

// One live state row of a project as Dapper reads it.
internal sealed record StateRow(Guid Id, string Type, string Name, string Category, int SortOrder);
