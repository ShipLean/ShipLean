namespace ShipLean.Contracts.Agents;

// PayloadId is what the agent passes as `payload`; UploadToken is the secret for the upload URL, shown once and stored only
// as a hash. UploadBy is when the upload URL stops working.
public sealed record IssuedPayload(string PayloadId, string UploadToken, DateTimeOffset UploadBy);
