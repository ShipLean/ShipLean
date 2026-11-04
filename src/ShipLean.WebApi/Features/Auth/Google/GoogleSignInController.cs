using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ShipLean.Application.Identity;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Auth.Google;

// GET /api/auth/google/start?returnUrl=  -> 302 to Google (anonymous; 404 while SHIPLEAN_GOOGLE_* isn't set). The login
// page's button is a plain link to it. Google comes back to GoogleSignInServices.CallbackPath, which the handler answers.
[Route("api/auth/google")]
public sealed class GoogleSignInController(
    GoogleSignInSettings settings,
    IOptionsMonitor<OpenIdConnectOptions> options,
    ILogger<GoogleSignInController> logger) : ApiControllerBase
{
    [AllowAnonymous]
    [HttpGet("start")]
    [ProducesResponseType(StatusCodes.Status302Found)]
    [ProducesResponseType<ApiError>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Start([FromQuery] string? returnUrl, CancellationToken cancellationToken)
    {
        if (!settings.IsEnabled)
        {
            throw ApiException.NotFound("Sign in with Google isn't turned on for this instance.");
        }
        // Local only. It travels in the handler's protected state, so this is the one place it needs checking.
        var local = Url.IsLocalUrl(returnUrl) ? returnUrl : "/";
        // The handler fetches Google's configuration on the challenge and lets a failure through as a 500, on a
        // full-page navigation. Fetch it here (the handler then finds it cached) so that failure, and only that, goes
        // back to the sign-in page like the callback's failures do. Only the type is logged: the message may hold a URL.
        string? failure = null;
        try
        {
            await options.Get(GoogleSignInServices.Scheme).ConfigurationManager!.GetConfigurationAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            failure = ex.GetType().Name;
        }
        if (failure is not null)
        {
            logger.LogWarning("Google sign-in could not start: Google's configuration was not fetched ({ErrorType})", failure);
            return Redirect(GoogleSignInEvents.LoginPath(GoogleSignInEvents.FailedState, local));
        }
        var properties = new AuthenticationProperties { RedirectUri = local };
        return Challenge(properties, GoogleSignInServices.Scheme);
    }
}
