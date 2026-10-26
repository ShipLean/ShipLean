using System.Threading.RateLimiting;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Features.Webhooks;

// The webhook endpoints are anonymous, so each address gets a budget: a busy org's deliveries come from a few provider
// addresses and fit well inside it; a guessing loop does not. Payloads are capped: a pull request event with a long
// description is tens of KiB. The rejection (429) is the sign-in module's global one.
public sealed class WebhookServices : IServiceModule
{
    public const string RateLimit = "webhooks";
    public const int RequestsPerMinutePerIp = 300;
    public const int MaxBodyBytes = 1024 * 1024;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddRateLimiter(options => options.AddPolicy(RateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinutePerIp, Window = TimeSpan.FromMinutes(1) })));
}
