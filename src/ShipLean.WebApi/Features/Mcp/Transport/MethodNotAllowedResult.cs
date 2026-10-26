using Microsoft.AspNetCore.Mvc;

namespace ShipLean.WebApi.Features.Mcp.Transport;

// A 405 that names the one method the route takes.
public sealed class MethodNotAllowedResult(string allow) : StatusCodeResult(StatusCodes.Status405MethodNotAllowed)
{
    public override void ExecuteResult(ActionContext context)
    {
        context.HttpContext.Response.Headers.Allow = allow;
        base.ExecuteResult(context);
    }
}
