using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// The agent acting on this request: the one the bearer scheme stored on the HttpContext, else no agent (a person using the
// app, or background work), so nothing is stamped. A singleton like ICurrentUser: it reads the request at call time.
public sealed class RequestCurrentAgent(IServiceProvider services) : ICurrentAgent
{
    public const string ItemKey = "shiplean.agent";

    private ICurrentAgent Current
        => services.GetService<IHttpContextAccessor>()?.HttpContext?.Items[ItemKey] as ICurrentAgent ?? AgentIdentity.None;

    public string? Name => Current.Name;

    public string? Host => Current.Host;

    public bool Verified => Current.Verified;
}
