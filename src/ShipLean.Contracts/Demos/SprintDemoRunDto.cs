namespace ShipLean.Contracts.Demos;

public sealed record SprintDemoRunDto(string ItemKey, string ItemUrl, string? SessionState, string RequestedLabel, string? UploadExpiresLabel);
