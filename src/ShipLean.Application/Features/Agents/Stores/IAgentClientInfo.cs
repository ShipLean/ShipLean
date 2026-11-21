namespace ShipLean.Application.Features.Agents.Stores;

// The name and version an MCP client sent in initialize, kept on its OAuth authorization (the transport is stateless). Self-reported.
public interface IAgentClientInfo
{
    Task RememberAsync(Guid authorizationId, string? name, string? version, CancellationToken cancellationToken = default);
}
