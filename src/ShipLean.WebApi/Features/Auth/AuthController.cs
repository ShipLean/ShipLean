using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Auth;
using ShipLean.Contracts.Errors;
using ShipLean.Contracts.Users;
using ShipLean.WebApi.Features.Auth.Proxy;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Auth;

// Sign in and out, change my password, end my other sessions, the instance's name for the sign-in page.
// POST /api/auth/login           LoginRequest -> MeDto (anonymous, rate limited; sets the session cookie)
// POST /api/auth/logout          -> 204 (clears the cookie)
// POST /api/auth/password        ChangePasswordRequest -> MeDto (fresh cookie; every other session ends)
// POST /api/auth/sign-out-others -> 204 (fresh cookie; every other session ends)
// GET  /api/auth/instance        -> InstanceDto (anonymous)
[Route("api/auth")]
public sealed class AuthController(
    SignInService signIn, CookieSession cookie, MeReader me, IUserAccounts accounts, ICurrentUser currentUser) : ApiControllerBase
{
    [AllowAnonymous]
    [PasswordSignInOnly]
    [EnableRateLimiting(AuthServices.LoginRateLimit)]
    [HttpPost("login")]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiError>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiError>(StatusCodes.Status429TooManyRequests)]
    public async Task<MeDto> Login(LoginRequest request, CancellationToken ct)
    {
        var userId = await signIn.SignInAsync(request, ct);
        await cookie.SignInAsync(userId, ct);
        return await me.GetAsync(userId, ct);
    }

    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public Task<NoContentResult> Logout() => NoContentAfter(cookie.SignOutAsync());

    // The new security stamp ends every other session; this one gets a fresh cookie under it.
    [AllowPasswordChange]
    [PasswordSignInOnly]
    [HttpPost("password")]
    [ProducesResponseType<MeDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiError>(StatusCodes.Status400BadRequest)]
    public async Task<MeDto> ChangePassword(ChangePasswordRequest request, CancellationToken ct)
    {
        var stamp = await accounts.ChangePasswordAsync(request.Current ?? string.Empty, request.New ?? string.Empty, ct);
        await cookie.SignInAsync(currentUser.Id, stamp);
        return await me.GetAsync(currentUser.Id, ct);
    }

    [HttpPost("sign-out-others")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<NoContentResult> SignOutOthers(CancellationToken ct)
    {
        await cookie.SignInAsync(currentUser.Id, await accounts.SignOutEverywhereElseAsync(ct));
        return NoContent();
    }

    [AllowAnonymous]
    [HttpGet("instance")]
    [ProducesResponseType<InstanceDto>(StatusCodes.Status200OK)]
    public Task<InstanceDto> Instance(CancellationToken ct) => me.InstanceAsync(ct);
}
