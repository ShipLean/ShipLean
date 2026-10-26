namespace ShipLean.Contracts.Agents;

// The Client ID Metadata Document fetch behind IOAuthClients.FindAsync for an https client_id.
public interface IClientMetadataDocuments
{
    // Null when the document is refused, for any reason and without saying which: the URL isn't https with a path; the host
    // resolves to a private, loopback or link-local address; the fetch redirects, times out or exceeds 64 KB; the document's
    // client_id isn't its URL; it declares a secret or private_key_jwt; it has no valid redirect_uris.
    Task<ClientMetadataDocument?> FetchAsync(string clientId, CancellationToken cancellationToken = default);
}
