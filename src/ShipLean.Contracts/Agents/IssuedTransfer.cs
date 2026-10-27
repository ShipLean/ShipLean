namespace ShipLean.Contracts.Agents;

// Token is the secret to put in a URL; it is shown once and stored only as a hash.
public sealed record IssuedTransfer(string Token, DateTimeOffset ExpiresAt);
