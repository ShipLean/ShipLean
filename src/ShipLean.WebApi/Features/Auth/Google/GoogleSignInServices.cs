using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using ShipLean.Application.Identity;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Auth.Google;

// "Sign in with Google": the OpenID Connect code flow against Google, ending in the ordinary session cookie
// (GoogleSignInEvents). The scheme is registered always, because configuration is read at resolve time, and removed at
// startup when SHIPLEAN_GOOGLE_* isn't set (GoogleSignInStartup).
public sealed class GoogleSignInServices : IServiceModule
{
    public const string Scheme = "Google";
    public const string Authority = "https://accounts.google.com";
    public const string CallbackPath = "/api/auth/google/callback";
    public const string HttpClientName = "google-sign-in";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(sp => GoogleSignInSettings.Parse(sp.GetRequiredService<IConfiguration>(), sp.GetRequiredService<ProxySignInSettings>()));
        services.AddHostedService<GoogleSignInStartup>();
        services.AddScoped<GoogleSignInEvents>();
        services.AddHttpClient(HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(10));
        services.AddAuthentication().AddOpenIdConnect(Scheme, _ => { });
        services.AddOptions<OpenIdConnectOptions>(Scheme).Configure<GoogleSignInSettings, IHttpClientFactory>((options, settings, clients) =>
        {
            options.Authority = Authority;
            options.ClientId = settings.ClientId;
            options.ClientSecret = settings.ClientSecret;
            options.CallbackPath = CallbackPath;
            options.ResponseType = OpenIdConnectResponseType.Code;
            // Query, not the handler's default form post, so the correlation and nonce cookies can stay SameSite=Lax:
            // Google's redirect back is a top-level GET.
            options.ResponseMode = OpenIdConnectResponseMode.Query;
            options.CorrelationCookie.SameSite = SameSiteMode.Lax;
            options.NonceCookie.SameSite = SameSiteMode.Lax;
            // The handler's defaults are openid and profile. Without email, Google's ID token has neither email claim.
            options.Scope.Add("email");
            options.Prompt = "select_account";
            options.MapInboundClaims = false;
            // Google documents both spellings of its issuer. The handler adds the discovery document's to these.
            options.TokenValidationParameters.ValidIssuers = ["accounts.google.com"];
            options.Backchannel = clients.CreateClient(HttpClientName);
            options.EventsType = typeof(GoogleSignInEvents);
        });
    }
}
