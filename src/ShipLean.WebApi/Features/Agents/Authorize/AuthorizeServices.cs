using System.Threading.RateLimiting;
using ShipLean.Contracts;
using ShipLean.WebApi.Features.Agents.Clients;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Authorize;

public sealed class AuthorizeServices : IServiceModule
{
    public const string RateLimit = "oauth-authorize";
    public const int RequestsPerMinutePerIp = 30;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddTransient<IStartupFilter, AntiFramingStartupFilter>();
        // Anonymous callers can make the server fetch a client metadata document, so the endpoint is limited per client address.
        // The 429 body comes from the limiter's shared OnRejected (AuthServices).
        services.AddRateLimiter(options => options.AddPolicy(RateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey.For(context.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinutePerIp, Window = TimeSpan.FromMinutes(1) })));
    }
}
