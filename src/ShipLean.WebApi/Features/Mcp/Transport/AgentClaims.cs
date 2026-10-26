using System.Security.Claims;
using ShipLean.Contracts.Agents;
using ShipLean.WebApi.Features.Mcp.Contracts;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// The claims of an agent's principal. NameIdentifier is the user id exactly as for a cookie user, so ICurrentUser reads it
// the same way; the scopes and the authorization ride along for the gate.
public static class AgentClaims
{
    public const string ScopeClaim = "shiplean:scope";
    public const string AuthorizationClaim = "shiplean:agent-authorization";

    public static ClaimsPrincipal For(AgentPrincipal agent)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.NameIdentifier, agent.UserId.ToString()),
            new(AuthorizationClaim, agent.AuthorizationId.ToString()),
        ];
        claims.AddRange(agent.Scopes.Order(StringComparer.Ordinal).Select(scope => new Claim(ScopeClaim, scope)));
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AgentPolicy.Scheme));
    }

    // A caller who arrived through the gateway email header: the person, with every tool, and no agent authorization.
    public static ClaimsPrincipal ForGateway(Guid userId)
    {
        Claim[] claims =
        [
            new(ClaimTypes.NameIdentifier, userId.ToString()),
            new(ScopeClaim, McpScopes.Read),
            new(ScopeClaim, McpScopes.Write),
            new(ScopeClaim, McpScopes.Plan),
        ];
        return new ClaimsPrincipal(new ClaimsIdentity(claims, AgentPolicy.Scheme));
    }

    public static IReadOnlyList<string> ScopesOf(ClaimsPrincipal user)
        => user.FindAll(ScopeClaim).Select(claim => claim.Value).ToList();
}
