namespace ShipLean.Contracts.Agents;

// What the payload upload endpoint answers. ExpiresAt is when the payload can no longer be used (Lifetime after the upload).
public sealed record UploadedPayloadDto(string Tool, string? Field, int SizeBytes, DateTimeOffset ExpiresAt);
