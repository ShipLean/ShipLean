using Dapper;
using Npgsql;

namespace ShipLean.TestSupport.Builders;

// Arranges one oauth_client row with parameterised SQL and returns its client_id. Defaults give a registered ("Test Agent")
// client with one loopback redirect; WithMetadataDocument makes it a verified one.
public sealed class OAuthClientBuilder
{
    private static int _counter;

    private readonly int _number = Interlocked.Increment(ref _counter);
    private string? _clientId;
    private string _kind = "registered";
    private string? _name;
    private string? _host;
    private string[] _redirectUris = ["http://127.0.0.1:8123/callback"];
    private DateTimeOffset? _metadataExpiresAt;
    private DateTimeOffset? _archivedAt;

    public OAuthClientBuilder WithClientId(string clientId)
    {
        _clientId = clientId;
        return this;
    }

    public OAuthClientBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public OAuthClientBuilder WithRedirectUris(params string[] redirectUris)
    {
        _redirectUris = redirectUris;
        return this;
    }

    // A client whose id is the URL of its metadata document, on a verified host.
    public OAuthClientBuilder WithMetadataDocument(string host, DateTimeOffset expiresAt)
    {
        _kind = "metadataDocument";
        _host = host;
        _metadataExpiresAt = expiresAt;
        _clientId ??= $"https://{host}/oauth/client-{_number}";
        return this;
    }

    public OAuthClientBuilder ArchivedAt(DateTimeOffset archivedAt)
    {
        _archivedAt = archivedAt;
        return this;
    }

    public string ClientId => _clientId ?? $"arranged-client-{_number}";

    public async Task<string> InsertAsync(NpgsqlConnection connection)
    {
        await connection.ExecuteAsync(
            """
            INSERT INTO oauth_client (client_id, kind, client_name, client_host, redirect_uris, metadata_expires_at, archived_at)
            VALUES (@clientId, @kind, @name, @host, @redirectUris, @metadataExpiresAt, @archivedAt)
            """,
            new
            {
                clientId = ClientId,
                kind = _kind,
                name = _name ?? $"Arranged Agent {_number}",
                host = _host,
                redirectUris = _redirectUris,
                metadataExpiresAt = _metadataExpiresAt,
                archivedAt = _archivedAt,
            });
        return ClientId;
    }
}
