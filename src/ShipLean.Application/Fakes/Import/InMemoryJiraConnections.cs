using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;

namespace ShipLean.Application.Fakes.Import;

// Hands back the credentials a test arranged for a connection id.
public sealed class InMemoryJiraConnections : IJiraConnections
{
    private readonly object _gate = new();
    private readonly Dictionary<Guid, JiraCredentials> _credentials = [];

    public void Arrange(Guid connectionId, JiraCredentials credentials)
    {
        lock (_gate)
        {
            _credentials[connectionId] = credentials;
        }
    }

    public Task<JiraCredentials> GetCredentialsAsync(Guid connectionId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return _credentials.TryGetValue(connectionId, out var credentials)
                ? Task.FromResult(credentials)
                : throw ApiException.NotFound("That Jira connection does not exist.");
        }
    }
}
