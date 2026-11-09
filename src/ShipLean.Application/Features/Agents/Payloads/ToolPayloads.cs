using System.Security.Cryptography;
using ShipLean.Application.Features.Agents.Stores;
using ShipLean.Contracts.Agents;

namespace ShipLean.Application.Features.Agents.Payloads;

// tool_payload: the payload id is 16 random bytes and the upload token 32, both base64url; only the token's SHA-256 is stored.
public sealed class ToolPayloads(TimeProvider clock) : IToolPayloads
{
    // TODO: store payloads in tool_payload (hash the token, claim once, enforce limits, bind lookups to user, authorization and tool).
    public Task<IssuedPayload> IssueAsync(NewToolPayload payload, CancellationToken cancellationToken = default)
        => Task.FromResult(new IssuedPayload(
            StoreSecrets.Base64Url(RandomNumberGenerator.GetBytes(16)), StoreSecrets.New(), clock.GetUtcNow().Add(IToolPayloads.UploadWindow)));

    // TODO: store payloads in tool_payload (hash the token, claim once, enforce limits, bind lookups to user, authorization and tool).
    public Task<UploadedPayloadDto> UploadAsync(string token, Stream body, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();

    // TODO: store payloads in tool_payload (hash the token, claim once, enforce limits, bind lookups to user, authorization and tool).
    public Task<ToolPayload?> FindAsync(string payloadId, string tool, Guid userId, Guid? authorizationId, CancellationToken cancellationToken = default)
        => throw new NotImplementedException();
}
