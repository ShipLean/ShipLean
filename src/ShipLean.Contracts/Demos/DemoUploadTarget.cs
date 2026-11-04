namespace ShipLean.Contracts.Demos;

public sealed record DemoUploadTarget(string Url, DateTimeOffset ExpiresAt, long MaxVideoBytes, string ItemKey, string SprintName);
