using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Inspection.AgentSetup;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Inspection;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Inspection;

[Route("api/inspection/projects/{project}/agent-setup")]
public sealed class InspectionAgentSetupController(AgentSetupService setup) : ApiControllerBase
{
    [HttpGet]
    [ProducesResponseType<AgentSetupDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<AgentSetupDto> Get(string project, [FromQuery] string agent, [FromQuery] string? item, CancellationToken cancellationToken)
        => setup.GetAsync(project, agent, item, cancellationToken);
}
