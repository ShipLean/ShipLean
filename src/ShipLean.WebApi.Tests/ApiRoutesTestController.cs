using Microsoft.AspNetCore.Mvc;
using ShipLean.Contracts;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Tests;

// A real controller route under /api, so MVC's own binding and routing failures can be checked against the ApiError
// shape. Only loaded by ApiErrorTests, which adds this assembly as an application part of its own test host.
[Route("api/test-controller/things")]
public sealed class ApiRoutesTestController : ApiControllerBase
{
    [HttpPost]
    public IActionResult Post(ThingBody body) => Ok(body);

    [HttpDelete("{id:guid}")]
    public IActionResult Delete(Guid id) => Ok(id);

    [HttpGet("origin")]
    public IActionResult Origin([FromServices] IRequestOrigin origin) => Ok(new { origin.ClientId });

    [HttpGet("category")]
    public IActionResult Category() => Ok(new { category = ShipLean.Contracts.Refs.StateCategory.InProgress });

    [HttpPost("category")]
    public IActionResult EchoCategory(CategoryBody body) => Ok(body);

    public sealed record ThingBody(string Title);

    public sealed record CategoryBody(ShipLean.Contracts.Refs.StateCategory Category);
}
