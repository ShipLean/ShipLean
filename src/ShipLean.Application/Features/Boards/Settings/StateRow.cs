namespace ShipLean.Application.Features.Boards.Settings;

// One live state row as Dapper reads it.
internal sealed record StateRow(Guid Id, string Type, string Name, string Category);
