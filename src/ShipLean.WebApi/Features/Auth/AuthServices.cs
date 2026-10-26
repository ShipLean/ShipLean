using System.Net;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Npgsql;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.Auth.Proxy;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Auth;

// Email-and-password sign-in, or sign-in through an identity proxy when a SHIPLEAN_AUTH_PROXY_* identity setting is
// present (ProxySignInSettings; either way the session is the same cookie). A Secure (over https), HttpOnly, SameSite=Lax cookie with a 14-day sliding expiry; the
// browser never holds a token. Every endpoint requires a signed-in user unless it opts out (AllowAnonymous: the sign-in
// page and endpoint, /api/health, /api/auth/instance); static assets are served before authorization runs. /api answers
// 401/403 instead of redirecting (CookieSessionEvents).
public sealed class AuthServices : IServiceModule
{
    public const string CookieScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    public const string CookieName = "shiplean.auth";
    public const string SessionScheme = "Session";
    public const string InstanceAdminPolicy = "InstanceAdmin";
    public const string LoginRateLimit = "login";
    public const int LoginsPerMinutePerIp = 30;
    public const string GenericRateLimitMessage = "Too many requests. Wait a moment and try again.";
    public const string TrustedProxiesKey = "SHIPLEAN_TRUSTED_PROXIES";
    public const string ForwardedForHeaderKey = "SHIPLEAN_FORWARDED_FOR_HEADER";

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        // Read at resolve time, so a test host's settings count; ProxySignInStartupLog resolves it at start, so a bad
        // setting fails startup naming it.
        services.AddSingleton(sp => ProxySignInSettings.Parse(sp.GetRequiredService<IConfiguration>()));
        services.AddHostedService<ProxySignInStartupLog>();
        services.AddAuthentication(options =>
            {
                options.DefaultScheme = SessionScheme;
                options.DefaultAuthenticateScheme = SessionScheme;
                options.DefaultChallengeScheme = CookieScheme;
                options.DefaultForbidScheme = CookieScheme;
                options.DefaultSignInScheme = CookieScheme;
                options.DefaultSignOutScheme = CookieScheme;
            })
            .AddCookie(CookieScheme, options =>
            {
                options.Cookie.Name = CookieName;
                options.Cookie.HttpOnly = true;
                // Secure whenever the request is https, which behind a TLS-terminating proxy is what X-Forwarded-Proto says.
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.LoginPath = "/login";
                options.ReturnUrlParameter = CookieSessionEvents.ReturnUrlParameter;
                options.EventsType = typeof(CookieSessionEvents);
            })
            // Who the request is: in proxy mode the proxy sign-in handler (it issues the ordinary cookie), except on the
            // paths agents reach without the proxy; otherwise, and always in password mode, the cookie. Everything else
            // is the cookie's.
            .AddPolicyScheme(SessionScheme, SessionScheme, options =>
            {
                options.ForwardDefaultSelector = context =>
                    context.RequestServices.GetRequiredService<ProxySignInSettings>().IsProxy && !ProxySignInPaths.IsSkipped(context.Request.Path)
                        ? ProxySignInHandler.Scheme
                        : CookieScheme;
                options.ForwardChallenge = CookieScheme;
                options.ForwardForbid = CookieScheme;
                options.ForwardSignIn = CookieScheme;
                options.ForwardSignOut = CookieScheme;
            })
            .AddScheme<AuthenticationSchemeOptions, ProxySignInHandler>(ProxySignInHandler.Scheme, _ => { });
        services.AddScoped<CookieSessionEvents>();
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(InstanceAdminPolicy, policy => policy.RequireAuthenticatedUser().AddRequirements(new InstanceAdminRequirement()));
        services.AddSingleton<IAuthorizationHandler, InstanceAdminAuthorizationHandler>();
        services.Configure<MvcOptions>(options => options.Filters.Add<PasswordChangeFilter>());

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            // A rate-limited sign-in gets the sentence every other sign-in failure gets; any other limited endpoint gets the generic message.
            options.OnRejected = async (context, ct) =>
            {
                var policy = context.HttpContext.GetEndpoint()?.Metadata.GetMetadata<EnableRateLimitingAttribute>()?.PolicyName;
                var message = policy == LoginRateLimit ? SignInService.Refusal : GenericRateLimitMessage;
                await ApiErrorWriter.WriteAsync(context.HttpContext.Response, ApiException.RateLimited(message), ct);
            };
            options.AddPolicy(LoginRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = LoginsPerMinutePerIp, Window = TimeSpan.FromMinutes(1) }));
        });

        // Behind a reverse proxy the app sees http; the forwarded proto makes the cookie Secure and URLs https, and the
        // forwarded client address (X-Forwarded-For, or the header named by SHIPLEAN_FORWARDED_FOR_HEADER, such as
        // CF-Connecting-IP) makes the login limiter partition on the real client. Only loopback and the addresses and
        // networks in SHIPLEAN_TRUSTED_PROXIES (comma-separated) are believed; anyone else's forwarded headers are
        // ignored. The host header is never forwarded.
        services.AddOptions<ForwardedHeadersOptions>().Configure<IConfiguration>((options, config) =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;
            if (config[ForwardedForHeaderKey]?.Trim() is { Length: > 0 } header)
            {
                options.ForwardedForHeaderName = header;
            }
            foreach (var entry in (config[TrustedProxiesKey] ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                if (IPAddress.TryParse(entry, out var address))
                {
                    options.KnownProxies.Add(address);
                }
                else if (System.Net.IPNetwork.TryParse(entry, out var network))
                {
                    options.KnownIPNetworks.Add(network);
                }
                else
                {
                    throw new InvalidOperationException($"{TrustedProxiesKey}: '{entry}' is not an address or a CIDR range.");
                }
            }
        });

        // The cookie's keys live in Postgres, so a redeploy does not sign everyone out.
        services.AddDataProtection().SetApplicationName("shiplean");
        services.AddOptions<KeyManagementOptions>()
            .Configure<NpgsqlDataSource>((options, dataSource) => options.XmlRepository = new PostgresXmlRepository(dataSource));
    }
}
