namespace ShipLean.Contracts.Inspection;

public sealed record InspectionItemInfo(Guid ProjectId, string ProjectKey, string ItemKey, DateTimeOffset CreatedAt);
