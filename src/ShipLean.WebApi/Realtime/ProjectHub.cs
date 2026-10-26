using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Realtime;

// /hubs/project: the transport for IProjectEvents (events only; no co-editing), over WebSockets only. A client calls
// JoinProject(projectId) and from then on receives that project's events as "projectEvent" messages carrying the
// ProjectEvent JSON (the same "type"-discriminated shape as ShipLean.Contracts.Events and
// src/ShipLean.Client/src/api/events.ts). It receives nothing for a project it has not joined. JoinScreen/LeaveScreen
// report which screen the caller is on, for presence.
//
// A failure is a HubException carrying "<code>: <message>"; SignalR wraps it for the client (docs/events.md). Access is
// checked on every JoinProject and JoinScreen call, so a member removed since the last call is refused. The user is
// always Context.User, never the request's IHttpContextAccessor. Who may connect at all (signed in, not on a one-time
// password, same-origin) is ProjectHubRequirement; a session that ends while the socket is open is HubSessionFilter
// and HubSessionSweep.
[Authorize]
public sealed partial class ProjectHub(HubMembership membership, ProjectGroupMembers members, IPresenceTracker presence, HubConnections connections) : Hub
{
    public const string Path = "/hubs/project";

    public const string EventMethod = "projectEvent";

    public static string GroupName(Guid projectId) => $"project:{projectId}";

    [GeneratedRegex("^[a-zA-Z]{1,40}$")]
    private static partial Regex ScreenIdPattern();

    public override async Task OnConnectedAsync()
    {
        connections.Opened(Context);
        await base.OnConnectedAsync();
    }

    public async Task JoinProject(Guid projectId)
    {
        await RequireMemberAsync(projectId);
        members.Joined(Context.ConnectionId, UserId, projectId);
        await Groups.AddToGroupAsync(Context.ConnectionId, GroupName(projectId));
        try
        {
            // A member removed between the check and the add must not stay in the group.
            await RequireMemberAsync(projectId);
        }
        catch (HubException)
        {
            await LeaveProject(projectId);
            throw;
        }
    }

    public async Task LeaveProject(Guid projectId)
    {
        members.Left(Context.ConnectionId, projectId);
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(projectId));
        await presence.LeaveProjectAsync(Context.ConnectionId, projectId);
    }

    public async Task JoinScreen(Guid projectId, string screenId)
    {
        await RequireMemberAsync(projectId);
        if (screenId is null || !ScreenIdPattern().IsMatch(screenId))
        {
            throw Refusal(ApiException.Validation("A screen id is 1 to 40 letters."));
        }
        await presence.JoinAsync(Context.ConnectionId, UserId, projectId, screenId);
        try
        {
            // A member removed between the check and the presence entry must not stay listed.
            await RequireMemberAsync(projectId);
        }
        catch (HubException)
        {
            await presence.LeaveAsync(Context.ConnectionId, projectId, screenId);
            throw;
        }
    }

    public Task LeaveScreen(Guid projectId, string screenId)
        => presence.LeaveAsync(Context.ConnectionId, projectId, screenId);

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Closed(Context.ConnectionId);
        members.Disconnected(Context.ConnectionId);
        await presence.DisconnectedAsync(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }

    private Guid UserId => Guid.Parse(Context.User!.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private async Task RequireMemberAsync(Guid projectId)
    {
        try
        {
            await membership.RequireMemberAsync(projectId, UserId, Context.ConnectionAborted);
        }
        catch (ApiException e)
        {
            throw Refusal(e);
        }
    }

    private static HubException Refusal(ApiException e) => new($"{e.Code}: {e.Message}");
}
