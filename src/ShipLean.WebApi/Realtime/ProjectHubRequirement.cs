using Microsoft.AspNetCore.Authorization;

namespace ShipLean.WebApi.Realtime;

// What the hub's negotiate and connect requests must satisfy beyond a signed-in user (the cookie handler already ends
// a session whose stamp changed with 401 session_ended). Refusals are ApiErrors, written by the pipeline.
//  - A user on a one-time password gets 403 password_change_required, as from a controller action.
//  - Origin (CSRF: the guard on /api does not cover /hubs): absent is allowed (non-browser clients); present must be
//    the request's own scheme, host and port after forwarded headers, else 403 forbidden.
public sealed class ProjectHubRequirement : IAuthorizationRequirement
{
}
