using Microsoft.AspNetCore.Mvc.Filters;

namespace ShipLean.WebApi.Features.Usage;

// TODO: track a web action as a usage event once it has run; for now it passes every action through.
public sealed class UsageActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        => await next();
}
