namespace ShipLean.Application.Features.Boards.Settings;

// One board row as Dapper reads it: the level and the settings arrive as text.
internal sealed record BoardRow(Guid Id, Guid ProjectId, string Level, string Settings, DateTimeOffset UpdatedAt);
