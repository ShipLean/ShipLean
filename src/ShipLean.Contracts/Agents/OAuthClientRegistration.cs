namespace ShipLean.Contracts.Agents;

// Dynamic client registration (RFC 7591): the name the client gives itself and where it may be sent back.
public sealed record OAuthClientRegistration(string ClientName, IReadOnlyList<string> RedirectUris);
