using ShipLean.Application.Identity;

namespace ShipLean.WebApi.Features.Auth.Proxy;

// Takes ProxySignInSettings at startup, so a bad SHIPLEAN_AUTH_PROXY_* setting fails host start naming it, and
// logs which sign-in is active. Never a token: only the header's name and the issuer.
public sealed class ProxySignInStartupLog(ProxySignInSettings settings, ILogger<ProxySignInStartupLog> logger) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        switch (settings.Mode)
        {
            case ProxySignInMode.Token:
                logger.LogInformation("Sign-in: proxy token from header {Header}, issuer {Issuer}", settings.TokenHeader, settings.Issuer);
                break;
            case ProxySignInMode.Header:
                logger.LogInformation("Sign-in: proxy header {Header}", settings.EmailHeader);
                break;
            default:
                logger.LogInformation("Sign-in: password");
                break;
        }
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
