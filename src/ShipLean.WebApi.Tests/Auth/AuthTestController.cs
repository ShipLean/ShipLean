using Microsoft.AspNetCore.Mvc;
using ShipLean.WebApi.Features.Auth;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Tests.Auth;

// Routes that exist only in the auth tests' hosts (added as an application part): one needing a signed-in user, one
// needing an instance admin. Production has no such endpoints.
[Route("api/test-auth")]
public sealed class AuthTestController : ApiControllerBase
{
    [HttpGet("anyone")]
    public IActionResult Anyone() => Ok();

    [InstanceAdmin]
    [HttpGet("admin")]
    public IActionResult Admin() => Ok();
}
