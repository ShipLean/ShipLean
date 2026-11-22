namespace ShipLean.Contracts.Retros.Analysis;

// Where the next page of an export starts: after the point logged at LoggedAt with this id (the export's order is logged time, then id).
public sealed record RetroExportCursor(DateTimeOffset LoggedAt, Guid PointId);
