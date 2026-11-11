using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.Contracts.WorkItems;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Usage;

// Tracks a web action once it has run, for the actions ActionUsageMap marks Track. The actor is read before the action runs
// (Logout ends the session). The outcome is read from the exception or the result: the response status is not written yet.
// An ApiException is left unhandled so ApiErrorServices still maps it. Anonymous requests have no actor and emit nothing.
// Extra properties come from the map (board), the command (IUsageScope.AddProperty) and the request (viewport); never from
// the route or the action's arguments.
public sealed class UsageActionFilter(IUsageEvents events, IUsageScope scope, TimeProvider time) : IAsyncActionFilter
{
    private const int ErrorStatus = 400;

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var key = KeyOf(context);
        var tracked = key is not null && ActionUsageMap.All.TryGetValue(key, out var usage) && usage.Kind == ActionUsageKind.Track ? usage : null;
        var actor = tracked is null ? null : ActorOf(context);
        if (key is null || tracked is null || actor is null)
        {
            await next();
            return;
        }

        var started = time.GetTimestamp();
        var executed = await next();
        var (outcome, errorCode) = OutcomeOf(executed);
        var properties = PropertiesOf(tracked, scope);
        events.Track(new UsageEvent(
            tracked.EventName!,
            UsageSurfaces.Of(context.HttpContext.User),
            actor,
            scope.ProjectId,
            scope.ProjectCount,
            outcome,
            errorCode,
            (long)time.GetElapsedTime(started).TotalMilliseconds,
            $"api:{key}",
            time.GetUtcNow(),
            properties));
    }

    // The board of a board move, then what the command added, then the request's viewport.
    private static Dictionary<string, object> PropertiesOf(ActionUsage tracked, IUsageScope scope)
    {
        var properties = new Dictionary<string, object>(StringComparer.Ordinal);
        if (tracked.Board is { } board)
        {
            properties[UsageProps.Board] = board;
        }
        foreach (var (key, value) in scope.Properties)
        {
            properties[key] = value;
        }
        if (scope.Viewport is { } viewport)
        {
            properties[UsageProps.Viewport] = viewport;
        }
        return properties;
    }

    private static string? KeyOf(ActionExecutingContext context)
        => context.ActionDescriptor is ControllerActionDescriptor descriptor
            ? $"{descriptor.ControllerTypeInfo.Name[..^"Controller".Length]}.{descriptor.MethodInfo.Name}"
            : null;

    private static UsageActor? ActorOf(ActionExecutingContext context)
    {
        var user = context.HttpContext.User;
        return user.Identity?.IsAuthenticated == true && Guid.TryParse(user.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? UsageActor.ForUser(id)
            : null;
    }

    private static (UsageOutcome Outcome, string? ErrorCode) OutcomeOf(ActionExecutedContext executed)
    {
        switch (executed.Exception)
        {
            case OperationCanceledException:
                return (UsageOutcome.Cancelled, null);
            case ApiException api:
                return (UsageOutcome.Error, api.Code);
            case not null:
                return (UsageOutcome.Error, ApiErrorCodes.Internal);
        }

        var status = executed.Result switch
        {
            IStatusCodeActionResult { StatusCode: { } statusCode } => statusCode,
            _ => 0,
        };
        if (status < ErrorStatus)
        {
            return (UsageOutcome.Ok, null);
        }
        var body = (executed.Result as ObjectResult)?.Value;
        var errorCode = body switch
        {
            ApiError error => error.Code,
            ConflictDto conflict => conflict.Code,
            _ => $"http_{status}",
        };
        return (UsageOutcome.Error, errorCode);
    }
}
