using System.Security.Cryptography;
using System.Text;

namespace ShipLean.Contracts.Agents;

// Reads of the instance's agent connections for the dispatch code (webhook signing, the GraphQL bearer check). An instance
// admin manages them through api/instance/agent-connections.
public interface IAgentConnections
{
    // The callback token as the store keeps it: the SHA-256 of its UTF-8 bytes.
    static byte[] HashCallbackToken(string? token) => SHA256.HashData(Encoding.UTF8.GetBytes(token ?? string.Empty));

    // Every connection, archived ones last, without secrets.
    Task<IReadOnlyList<AgentConnectionDto>> ListAsync(CancellationToken cancellationToken = default);

    // One connection by id with its signing secret decrypted, archived or not (a session started before the archive still
    // signs); null when there is none. Throws ApiException.NotConfigured when the saved secret can no longer be decrypted.
    Task<AgentConnection?> GetAsync(Guid id, CancellationToken cancellationToken = default);

    // The live (not archived) connection whose callback token hashes to this, or null. Hash with HashCallbackToken. It
    // never decrypts the signing secret, so it works with a lost key ring; use GetAsync for the secret.
    Task<AgentConnectionIdentity?> FindActiveByCallbackTokenHashAsync(byte[] tokenHash, CancellationToken cancellationToken = default);
}
