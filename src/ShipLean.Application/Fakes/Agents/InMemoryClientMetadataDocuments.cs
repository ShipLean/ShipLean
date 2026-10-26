using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Fakes.Agents;

// Knows exactly one document: InMemoryAgentStore.ClaudeCode's. Every other id, and any id that isn't https, answers null.
public sealed class InMemoryClientMetadataDocuments(TimeProvider clock) : IClientMetadataDocuments
{
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public Task<ClientMetadataDocument?> FetchAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var known = InMemoryAgentStore.ClaudeCode;
        var found = Uri.TryCreate(clientId, UriKind.Absolute, out var uri)
            && uri.Scheme == Uri.UriSchemeHttps
            && uri.AbsolutePath.Length > 1
            && string.Equals(clientId, known.ClientId, StringComparison.Ordinal);
        return Task.FromResult(found ? new ClientMetadataDocument(known, clock.GetUtcNow() + Lifetime) : null);
    }
}
