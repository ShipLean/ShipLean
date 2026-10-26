using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts.Agents;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Clients;

// RFC 8414. The protected-resource metadata is the MCP SDK's (see AgentPolicyServices); this is the other half. Anonymous:
// an agent reads it before it has any credentials.
[AllowAnonymous]
[Route(".well-known/oauth-authorization-server")]
public sealed class AuthorizationServerMetadataController(IAgentAddresses addresses) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<AuthorizationServerMetadataDto>(StatusCodes.Status200OK)]
    public async Task<AuthorizationServerMetadataDto> Get(CancellationToken cancellationToken)
    {
        var issuer = (await addresses.ForCurrentRequestAsync(cancellationToken)).Issuer;
        return new AuthorizationServerMetadataDto(
            issuer,
            issuer + AgentUrls.AuthorizePath,
            issuer + AgentUrls.TokenPath,
            issuer + AgentUrls.RevokePath,
            issuer + AgentUrls.RegisterPath,
            ["code"],
            ["authorization_code", "refresh_token"],
            ["S256"],
            ["none"],
            true,
            true);
    }
}
