using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// The agent acting on this request: the one the bearer scheme stored in the request's items, else no agent (a person using the
// app, or background work), so nothing is stamped. A singleton like ICurrentUser: it reads the request at call time.
public sealed class RequestCurrentAgent(ICurrentRequest request) : ICurrentAgent
{
    public const string ItemKey = "shiplean.agent";

    private ICurrentAgent Current
        => request.Items?[ItemKey] as ICurrentAgent ?? AgentIdentity.None;

    public string? Name => Current.Name;

    public string? Host => Current.Host;

    public bool Verified => Current.Verified;
}
