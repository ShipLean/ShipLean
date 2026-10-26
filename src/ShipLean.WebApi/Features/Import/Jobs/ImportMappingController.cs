using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Features.Import.Jobs;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Import;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Import.Jobs;

// The mapping the stepper starts from: suggested for a Jira project, or, for a re-run, with a finished job's choices kept. Project admins only.
[Route("api/import")]
public sealed class ImportMappingController(ImportJobCommands commands) : ApiControllerBase
{
    [HttpPost("mapping")]
    [ProducesResponseType<ImportMappingDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public Task<ImportMappingDto> Suggest(ImportMappingRequest request, CancellationToken cancellationToken)
        => commands.SuggestMappingAsync(request.ConnectionId, request.JiraProjectKey, request.PreviousJobId, cancellationToken);
}
