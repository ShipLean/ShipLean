using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Usage;
using ShipLean.Contracts.WorkItems;

namespace ShipLean.WebApi.Features.Usage;

// Tracks a web action once it has run, for the actions ActionUsageMap marks Track. The actor is read before the action runs
// (Logout ends the session). The outcome is read from the exception or the result: the response status is not written yet.
// An ApiException is left unhandled so ApiErrorServices still maps it. Anonymous requests have no actor and emit nothing.
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
        var properties = tracked.Board is { } board
            ? new Dictionary<string, object> { [UsageProps.Board] = board }
            : [];
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
