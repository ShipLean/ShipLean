namespace ShipLean.Contracts.Agents;

// How the client became known: its client_id is the URL of a metadata document ShipLean fetched (its host is verified),
// or it registered itself through dynamic client registration (nothing about it is verified).
public enum OAuthClientKind
{
    MetadataDocument,
    Registered,
}
