namespace ShipLean.Contracts.Import;

// Where the importer gets a connection's Jira credentials. Server-side only: no endpoint returns them, and the token is
// never read back through the API. Callers check project access first. Throws ApiException.NotFound for an unknown or
// archived connection, or one with no token saved.
public interface IJiraConnections
{
    Task<JiraCredentials> GetCredentialsAsync(Guid connectionId, CancellationToken cancellationToken = default);
}
