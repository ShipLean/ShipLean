using Microsoft.AspNetCore.Authentication;
using ShipLean.Application.Identity;

namespace ShipLean.WebApi.Features.Auth.Google;

// Takes GoogleSignInSettings at startup, so a bad SHIPLEAN_GOOGLE_* setting fails host start naming it. With Google off
// it removes the scheme: a remote scheme's handler runs on every request, and it can't be built without a client id.
public sealed class GoogleSignInStartup(GoogleSignInSettings settings, IAuthenticationSchemeProvider schemes, ILogger<GoogleSignInStartup> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (settings.IsEnabled)
        {
            logger.LogInformation("Sign-in: Google is on, and anyone with a Google account can create a user");
        }
        else
        {
            schemes.RemoveScheme(GoogleSignInServices.Scheme);
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
