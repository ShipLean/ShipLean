using System.Collections.Concurrent;
using System.Text;
using ShipLean.Contracts.Agents;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// IToolPayloads for the payload filter tests: payloads are added straight in (no upload), each bound to a tool, a user and an
// authorization (null for a gateway caller), and FindAsync answers the way the contract says, recording every lookup.
internal sealed class FakeToolPayloads : IToolPayloads
{
    private readonly ConcurrentDictionary<string, (ToolPayload Payload, Guid UserId, Guid? AuthorizationId)> _payloads = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<Lookup> _lookups = new();

    public IReadOnlyList<Lookup> Lookups => [.. _lookups];

    public string AddObject(string tool, string json, Guid userId, Guid? authorizationId) => Add(new ToolPayload(tool, null, Encoding.UTF8.GetBytes(json)), userId, authorizationId);

    public string AddField(string tool, string field, string text, Guid userId, Guid? authorizationId) => Add(new ToolPayload(tool, field, Encoding.UTF8.GetBytes(text)), userId, authorizationId);

    public Task<IssuedPayload> IssueAsync(NewToolPayload payload, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The payload filter tests add payloads directly.");

    public Task<UploadedPayloadDto> UploadAsync(string token, Stream body, CancellationToken cancellationToken = default)
        => throw new NotSupportedException("The payload filter tests add payloads directly.");

    public Task<ToolPayload?> FindAsync(string payloadId, string tool, Guid userId, Guid? authorizationId, CancellationToken cancellationToken = default)
    {
        _lookups.Enqueue(new Lookup(payloadId, tool, userId, authorizationId));
        return Task.FromResult(
            _payloads.TryGetValue(payloadId, out var stored) && stored.Payload.Tool == tool && stored.UserId == userId && stored.AuthorizationId == authorizationId
                ? stored.Payload
                : null);
    }

    private string Add(ToolPayload payload, Guid userId, Guid? authorizationId)
    {
        var id = $"pl_{Guid.NewGuid():N}";
        _payloads[id] = (payload, userId, authorizationId);
        return id;
    }

    public sealed record Lookup(string PayloadId, string Tool, Guid UserId, Guid? AuthorizationId);
}
