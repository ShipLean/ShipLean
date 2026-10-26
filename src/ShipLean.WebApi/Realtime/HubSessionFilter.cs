using Microsoft.AspNetCore.SignalR;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Realtime;

// Runs before every hub method: a connection whose session has ended is aborted and refused (session_ended); one whose
// user is on a one-time password is refused (password_change_required), as a controller action would.
public sealed class HubSessionFilter(HubSessionCheck check) : IHubFilter
{
    public async ValueTask<object?> InvokeMethodAsync(HubInvocationContext invocationContext, Func<HubInvocationContext, ValueTask<object?>> next)
    {
        if (await check.RefusalAsync(invocationContext.Context.User, invocationContext.Context.ConnectionAborted) is { } refusal)
        {
            if (refusal.Code == ApiErrorCodes.SessionEnded)
            {
                invocationContext.Context.Abort();
            }
            throw new HubException($"{refusal.Code}: {refusal.Message}");
        }
        return await next(invocationContext);
    }
}
