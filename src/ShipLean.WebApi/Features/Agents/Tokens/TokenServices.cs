using System.Threading.RateLimiting;
using ShipLean.Contracts;
using ShipLean.WebApi.Features.Agents.Clients;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.Agents.Tokens;

// The token and revocation endpoints are anonymous, so each IP gets a budget shared by both. A refresh every few minutes
// from several agents behind one address fits well inside it; a guessing loop does not. The rejection (429) is the
// sign-in module's global one.
public sealed class TokenServices : IServiceModule
{
    public const string TokenRateLimit = "oauth-token";
    public const int RequestsPerMinutePerIp = 60;
    public const int MaxBodyBytes = 16 * 1024;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddRateLimiter(options => options.AddPolicy(TokenRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey.For(context.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinutePerIp, Window = TimeSpan.FromMinutes(1) })));
}
