using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Auth.Google;

// GET /api/auth/google/start?returnUrl=  -> 302 to Google (anonymous; 404 while SHIPLEAN_GOOGLE_* isn't set). The login
// page's button is a plain link to it. Google comes back to GoogleSignInServices.CallbackPath, which the handler answers.
[Route("api/auth/google")]
public sealed class GoogleSignInController(GoogleSignInSettings settings) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("start")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public IActionResult Start([FromQuery] string? returnUrl)
    {
        if (!settings.IsEnabled)
        {
            throw ApiException.NotFound("Sign in with Google isn't turned on for this instance.");
        }
        // Local only. It travels in the handler's protected state, so this is the one place it needs checking.
        var properties = new AuthenticationProperties { RedirectUri = Url.IsLocalUrl(returnUrl) ? returnUrl : "/" };
        return Challenge(properties, GoogleSignInServices.Scheme);
    }
}
