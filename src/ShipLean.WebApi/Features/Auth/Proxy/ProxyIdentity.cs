namespace ShipLean.WebApi.Features.Auth.Proxy;

// A verified proxy identity. Email is already clean (IdentityInput.Email); Name is clean (IdentityInput.Name) or null.
public sealed record ProxyIdentity(string Email, string? Name);
