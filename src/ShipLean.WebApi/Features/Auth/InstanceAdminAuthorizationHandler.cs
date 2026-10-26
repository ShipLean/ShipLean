using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ShipLean.Application.Identity;

namespace ShipLean.WebApi.Features.Auth;

// Reads is_admin and disabled_at from app_user on every request, never from a claim and never from the cache:
// demoting an instance admin does not rotate their stamp, so their session lives on and only this check stops them.
public sealed class InstanceAdminAuthorizationHandler(UserSessions sessions) : AuthorizationHandler<InstanceAdminRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, InstanceAdminRequirement requirement)
    {
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && await sessions.GetFreshAsync(userId) is { IsAdmin: true, Disabled: false })
        {
            context.Succeed(requirement);
        }
    }
}
