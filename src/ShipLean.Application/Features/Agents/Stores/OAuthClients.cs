using Dapper;
using Npgsql;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Stores;

// Registered clients live in oauth_client. A metadata-document client_id (an https URL) is fetched through
// IClientMetadataDocuments and kept in the same table until metadata_expires_at, so a consent or an authorization still
// has a client row to point at.
public sealed class OAuthClients(NpgsqlDataSource dataSource, IClientMetadataDocuments documents, TimeProvider clock) : IOAuthClients
{
    private sealed record Stored(
        string ClientId,
        string Kind,
        string ClientName,
        string? ClientHost,
        Array RedirectUris,
        DateTimeOffset? MetadataExpiresAt,
        DateTimeOffset? ArchivedAt)
    {
        public ClientRow ToRow() => new(ClientId, Kind, ClientName, ClientHost, (string[])RedirectUris);
    }

    public async Task<OAuthClient?> FindAsync(string clientId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(clientId))
        {
            return null;
        }
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        var stored = await connection.QuerySingleOrDefaultAsync<Stored>(new CommandDefinition(
            """
            SELECT client_id AS clientId, kind, client_name AS clientName, client_host AS clientHost, redirect_uris AS redirectUris,
                   metadata_expires_at AS metadataExpiresAt, archived_at AS archivedAt
            FROM oauth_client WHERE client_id = @clientId
            """,
            new { clientId }, cancellationToken: cancellationToken));
        if (stored?.ArchivedAt is not null)
        {
            return null;
        }
        if (stored is { Kind: "registered" } || (stored?.MetadataExpiresAt is { } expires && expires > clock.GetUtcNow()))
        {
            return stored.ToRow().ToClient();
        }
        if (!Uri.TryCreate(clientId, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            return null;
        }

        var document = await documents.FetchAsync(clientId, cancellationToken);
        if (document is not { Client: { Kind: OAuthClientKind.MetadataDocument, ClientHost: not null } client } || client.ClientId != clientId)
        {
            return null;
        }
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO oauth_client (client_id, kind, client_name, client_host, redirect_uris, metadata_expires_at, created_at)
            VALUES (@clientId, 'metadataDocument', @name, @host, @redirects, @expires, @now)
            ON CONFLICT (client_id) DO UPDATE
                SET client_name = excluded.client_name, client_host = excluded.client_host,
                    redirect_uris = excluded.redirect_uris, metadata_expires_at = excluded.metadata_expires_at
                WHERE oauth_client.kind = 'metadataDocument' AND oauth_client.archived_at IS NULL
            """,
            new
            {
                clientId,
                name = client.ClientName,
                host = client.ClientHost,
                redirects = client.RedirectUris.ToArray(),
                expires = document.ExpiresAt,
                now = clock.GetUtcNow(),
            },
            cancellationToken: cancellationToken));
        return client;
    }

    public async Task<OAuthClient> RegisterAsync(OAuthClientRegistration request, CancellationToken cancellationToken = default)
    {
        var (name, redirects) = ClientRegistrationRules.Check(request);
        var clientId = StoreSecrets.New();
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await connection.ExecuteAsync(new CommandDefinition(
            """
            INSERT INTO oauth_client (client_id, kind, client_name, client_host, redirect_uris, created_at)
            VALUES (@clientId, 'registered', @name, NULL, @redirects, @now)
            """,
            new { clientId, name, redirects, now = clock.GetUtcNow() }, cancellationToken: cancellationToken));
        return new OAuthClient(clientId, OAuthClientKind.Registered, name, null, redirects);
    }
}
