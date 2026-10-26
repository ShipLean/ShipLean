using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Fakes.Agents;

// The clients, authorizations, codes and tokens the five in-memory agent fakes share, so revoking an authorization in
// one is seen by the others. One lock guards everything; nothing is removed, revoked rows keep a revoked_at. Secrets are
// kept as SHA-256 hashes, found with a constant-time comparison.
public sealed class InMemoryAgentStore
{
    internal sealed class Authorization
    {
        public required Guid Id { get; init; }
        public required Guid UserId { get; init; }
        public required string ClientId { get; init; }
        public required string ScopeText { get; set; }
        public required string SecurityStamp { get; set; }
        public required DateTimeOffset CreatedAt { get; init; }
        public DateTimeOffset? LastUsedAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
    }

    internal sealed class Code
    {
        public required byte[] SecretHash { get; init; }
        public required Guid AuthorizationId { get; init; }
        public required string CodeChallenge { get; init; }
        public required string RedirectUri { get; init; }
        public required string Resource { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset? UsedAt { get; set; }
    }

    internal sealed class Token
    {
        public required byte[] SecretHash { get; init; }
        public required bool IsRefresh { get; init; }
        public required Guid AuthorizationId { get; init; }
        public required Guid FamilyId { get; init; }
        public required string Resource { get; init; }
        public required DateTimeOffset ExpiresAt { get; init; }
        public DateTimeOffset? RotatedAt { get; set; }
        public DateTimeOffset? RevokedAt { get; set; }
    }

    internal object Gate { get; } = new();

    internal Dictionary<string, OAuthClient> Clients { get; } = new(StringComparer.Ordinal)
    {
        [ClaudeCode.ClientId] = ClaudeCode,
        [TestAgent.ClientId] = TestAgent,
    };

    internal List<Authorization> Authorizations { get; } = [];

    internal List<Code> Codes { get; } = [];

    internal List<Token> Tokens { get; } = [];

    // An archived client is unknown to the fakes, as to the real store's lookups.
    public void Archive(string clientId)
    {
        lock (Gate)
        {
            Clients.Remove(clientId);
        }
    }

    public static OAuthClient ClaudeCode { get; } = new(
        "https://claude.ai/oauth/claude-code-client-metadata", OAuthClientKind.MetadataDocument, "Claude Code", "claude.ai",
        ["https://claude.ai/api/mcp/auth_callback", "http://localhost:8080/callback"]);

    public static OAuthClient TestAgent { get; } = new(
        "test-agent", OAuthClientKind.Registered, "Test Agent", null, ["http://127.0.0.1:8123/callback"]);
}
