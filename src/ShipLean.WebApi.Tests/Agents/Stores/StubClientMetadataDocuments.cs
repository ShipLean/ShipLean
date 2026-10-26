using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Tests.Agents.Stores;

// Serves exactly one metadata document, so the store tests do not depend on what the app's fake knows.
internal sealed class StubClientMetadataDocuments(TimeProvider clock) : IClientMetadataDocuments
{
    public const string DocumentUrl = "https://agent.example.test/client.json";
    public const string Name = "Stub Agent";
    public static readonly TimeSpan Lifetime = TimeSpan.FromHours(1);

    public Task<ClientMetadataDocument?> FetchAsync(string clientId, CancellationToken cancellationToken = default)
    {
        var client = new OAuthClient(DocumentUrl, OAuthClientKind.MetadataDocument, Name, "agent.example.test", ["http://127.0.0.1:8123/callback"]);
        return Task.FromResult(clientId == DocumentUrl ? new ClientMetadataDocument(client, clock.GetUtcNow() + Lifetime) : null);
    }
}
