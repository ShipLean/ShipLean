using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Stores;

// The oauth_client columns of a client as Dapper maps them (by constructor parameter name).
internal sealed record ClientRow(string ClientId, string Kind, string ClientName, string? ClientHost, string[] RedirectUris)
{
    public OAuthClient ToClient()
        => new(ClientId, Kind == "metadataDocument" ? OAuthClientKind.MetadataDocument : OAuthClientKind.Registered, ClientName, ClientHost, RedirectUris);
}
