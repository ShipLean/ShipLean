namespace ShipLean.Contracts.Agents;

// A fetched and validated Client ID Metadata Document: the client it describes (Kind = MetadataDocument, ClientHost = the
// document URL's host) and when the answer should be fetched again.
public sealed record ClientMetadataDocument(OAuthClient Client, DateTimeOffset ExpiresAt);
