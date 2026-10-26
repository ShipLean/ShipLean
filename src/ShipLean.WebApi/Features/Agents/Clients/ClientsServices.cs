using System.Threading.RateLimiting;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Agents.Clients;

// Registration is anonymous, so each IP (an IPv6 caller's /64) gets a small budget. The rejection (429) is the sign-in module's global one.
public sealed class ClientsServices : IServiceModule
{
    public const string RegisterRateLimit = "oauth-register";
    public const int RegistrationsPerMinutePerIp = 10;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddRateLimiter(options => options.AddPolicy(RegisterRateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey.For(context.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = RegistrationsPerMinutePerIp, Window = TimeSpan.FromMinutes(1) })));
}
