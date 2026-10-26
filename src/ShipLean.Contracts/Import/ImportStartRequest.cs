namespace ShipLean.Contracts.Import;

public sealed record ImportStartRequest(Guid ConnectionId, string JiraProjectKey, ImportTarget Target, ImportMappingDto Mapping);
