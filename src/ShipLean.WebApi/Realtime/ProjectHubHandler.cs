using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;

namespace ShipLean.WebApi.Realtime;

// Enforces ProjectHubRequirement (see there).
public sealed class ProjectHubHandler(UserSessions sessions) : AuthorizationHandler<ProjectHubRequirement>
{
    protected override async Task HandleRequirementAsync(AuthorizationHandlerContext context, ProjectHubRequirement requirement)
    {
        if (context.Resource is not HttpContext http)
        {
            throw new InvalidOperationException("The hub requirement needs the request as the authorization resource.");
        }
        RequireSameOrigin(http.Request);
        if (Guid.TryParse(context.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)
            && await sessions.GetAsync(userId, http.RequestAborted) is { MustChangePassword: true })
        {
            throw ApiException.PasswordChangeRequired();
        }
        context.Succeed(requirement);
    }

    private static void RequireSameOrigin(HttpRequest request)
    {
        var origin = request.Headers.Origin;
        if (origin.Count == 0)
        {
            return;
        }
        var scheme = request.Scheme;
        var port = request.Host.Port ?? (scheme == Uri.UriSchemeHttps ? 443 : 80);
        if (origin.Count == 1
            && Uri.TryCreate(origin[0], UriKind.Absolute, out var uri)
            && uri.Scheme == scheme
            && string.Equals(uri.Host, request.Host.Host, StringComparison.OrdinalIgnoreCase)
            && uri.Port == port
            && uri.PathAndQuery == "/"
            && uri.UserInfo.Length == 0)
        {
            return;
        }
        throw ApiException.Forbidden("This connection was refused: its origin is not this site.");
    }
}
