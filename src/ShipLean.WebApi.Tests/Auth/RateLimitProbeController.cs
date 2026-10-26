using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Tests.Auth;

// A route that exists only in the rate-limit test's host (added as an application part): limited by a policy other than
// the sign-in one, which the test registers.
[Route("api/test-limit")]
public sealed class RateLimitProbeController : ApiControllerBase
{
    public const string Policy = "probe";

    [AllowAnonymous]
    [EnableRateLimiting(Policy)]
    [HttpGet("once")]
    public IActionResult Once() => Ok();
}
