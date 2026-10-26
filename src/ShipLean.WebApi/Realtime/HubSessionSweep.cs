using System.Security.Claims;
using Microsoft.AspNetCore.SignalR;

namespace ShipLean.WebApi.Realtime;

// Once a minute, re-checks the session of every open hub connection and aborts the ones that fail, so a user who was
// disabled or signed out elsewhere stops receiving within the UserSessions freshness window even if they never call
// the hub again. It also re-checks the membership of every project a connection here joined and drops the users who
// are no longer members: the backstop for a member removal that never reached this instance (IMemberRemovals).
public sealed class HubSessionSweep(
    HubConnections connections,
    HubSessionCheck check,
    ProjectGroupMembers members,
    HubMembership membership,
    TimeProvider clock,
    ILogger<HubSessionSweep> logger) : BackgroundService
{
    public static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Period, clock);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await SweepAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException || !stoppingToken.IsCancellationRequested)
            {
                // A failed round must not stop the host; the next tick tries again.
                logger.LogWarning(e, "The hub session sweep failed; trying again next round.");
            }
        }
    }

    public async Task SweepAsync(CancellationToken cancellationToken)
    {
        foreach (var context in connections.Snapshot())
        {
            try
            {
                await AbortIfEndedAsync(context, cancellationToken);
            }
            catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
            {
                // The session could not be read (the database blipped): leave this connection open this round.
                logger.LogWarning(e, "The hub session sweep could not check the session of user {UserId}; leaving the connection open.",
                    context.User?.FindFirstValue(ClaimTypes.NameIdentifier));
            }
        }
        try
        {
            await DropRemovedMembersAsync(cancellationToken);
        }
        catch (Exception e) when (e is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(e, "The hub session sweep could not check project memberships; trying again next round.");
        }
    }

    private async Task AbortIfEndedAsync(HubCallerContext context, CancellationToken cancellationToken)
    {
        if (await check.RefusalAsync(context.User, cancellationToken) is not null)
        {
            context.Abort();
        }
    }

    private async Task DropRemovedMembersAsync(CancellationToken cancellationToken)
    {
        foreach (var (projectId, userId) in await membership.NotMembersAsync(members.Memberships(), cancellationToken))
        {
            await members.RemoveUserAsync(projectId, userId);
        }
    }
}
