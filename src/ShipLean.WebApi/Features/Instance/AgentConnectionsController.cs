using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Instance.AgentConnections;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Instance;

// The instance admin's Agents tab: Linear-compatible connections. The signing secret and callback token are in the answer
// to the call that made them, once; every read has their last four characters only.
// GET  api/instance/agent-connections                          -> AgentConnectionDto[]
// POST api/instance/agent-connections                          -> AgentConnectionIssuedDto (secret and token)
// PUT  api/instance/agent-connections/{id}                     -> AgentConnectionDto
// POST api/instance/agent-connections/{id}/regenerate-secret   -> AgentConnectionIssuedDto (secret)
// POST api/instance/agent-connections/{id}/regenerate-token    -> AgentConnectionIssuedDto (token)
// POST api/instance/agent-connections/{id}/archive             -> AgentConnectionDto
// GET  api/instance/agent-connections/{id}/health              -> AgentConnectionHealthDto
// POST api/instance/agent-connections/{id}/test                -> AgentConnectionTestDto (a signed ping with no item data)
[InstanceAdmin]
[Route("api/instance/agent-connections")]
public sealed class AgentConnectionsController(IAgentConnections connections, AgentConnectionAdmin admin, IAgentConnectionHealth health, IAgentConnectionTester tester) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AgentConnectionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<IReadOnlyList<AgentConnectionDto>> List(CancellationToken cancellationToken) => connections.ListAsync(cancellationToken);

    [HttpPost]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<AgentConnectionIssuedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<AgentConnectionIssuedDto> Create(AgentConnectionPut body, CancellationToken cancellationToken) => admin.CreateAsync(body, cancellationToken);

    [HttpPut("{id:guid}")]
    [ProducesResponseType<AgentConnectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<AgentConnectionDto> Update(Guid id, AgentConnectionPut body, CancellationToken cancellationToken) => admin.UpdateAsync(id, body, cancellationToken);

    [HttpPost("{id:guid}/regenerate-secret")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<AgentConnectionIssuedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<AgentConnectionIssuedDto> RegenerateSecret(Guid id, CancellationToken cancellationToken) => admin.RegenerateSecretAsync(id, cancellationToken);

    [HttpPost("{id:guid}/regenerate-token")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<AgentConnectionIssuedDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiError>(StatusCodes.Status409Conflict)]
    public Task<AgentConnectionIssuedDto> RegenerateToken(Guid id, CancellationToken cancellationToken) => admin.RegenerateTokenAsync(id, cancellationToken);

    [HttpPost("{id:guid}/archive")]
    [ProducesResponseType<AgentConnectionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AgentConnectionDto> Archive(Guid id, CancellationToken cancellationToken) => admin.ArchiveAsync(id, cancellationToken);

    [HttpGet("{id:guid}/health")]
    [ProducesResponseType<AgentConnectionHealthDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    public Task<AgentConnectionHealthDto> Health(Guid id, CancellationToken cancellationToken) => health.ForConnectionAsync(id, cancellationToken);

    [HttpPost("{id:guid}/test")]
    [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
    [ProducesResponseType<AgentConnectionTestDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AgentConnectionTestDto> Test(Guid id, CancellationToken cancellationToken) => tester.TestAsync(id, cancellationToken);
}
