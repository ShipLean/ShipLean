using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Tests.Usage;

// A route that exists only in the usage tests' host (added as an application part): it passes the access check for each project
// named, swallows a refusal, adds count as an extra property when given, and says what the request's usage scope holds
// afterwards. Production has no such endpoint.
[Route("api/test-usage")]
public sealed class UsageProbeController(IProjectAccess access, IUsageScope scope) : ApiControllerBase
{
    [HttpGet("touch")]
    public async Task<UsageProbeResult> Touch([FromQuery] Guid[] project, [FromQuery] long? count, CancellationToken cancellationToken)
    {
        if (count is { } value)
        {
            scope.AddProperty(UsageProps.Count, value);
        }
        foreach (var id in project)
        {
            try
            {
                await access.RequireAsync(id, ProjectRole.Member, cancellationToken);
            }
            catch (ApiException)
            {
                // A refused check is part of what the test observes: the scope must stay clear of it.
            }
        }

        return new UsageProbeResult(scope.ProjectId, scope.ProjectCount, scope.Viewport, scope.Properties);
    }
}
