using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// The transport is stateless: there is no GET stream and no DELETE session, so /mcp answers 405 to anything but POST. The
// SDK maps only POST, and any other method would otherwise fall through to the SPA's catch-all, which redirects an agent
// to /login. This catches the other methods, behind the same policy as POST: no token is still a 401. It lives at /mcp, not
// under api/, because the route it answers is the SDK's own and only an exact /mcp route can catch the non-POST methods;
// the api/ 404 fallback and the access sweep's route coverage do not apply to it.
[Route("mcp")]
[Authorize(Policy = AgentPolicy.Name)]
public sealed class McpMethodsController : ApiControllerBase
{
    [AcceptVerbs("GET", "HEAD", "PUT", "PATCH", "DELETE")]
    [ProducesResponseType(StatusCodes.Status405MethodNotAllowed)]
    public MethodNotAllowedResult MethodNotAllowed() => new("POST");
}
