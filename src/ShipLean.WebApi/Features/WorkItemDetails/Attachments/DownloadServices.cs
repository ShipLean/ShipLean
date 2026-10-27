using System.Threading.RateLimiting;
using ShipLean.Contracts;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Attachments;

// The download endpoint is anonymous (the URL's token is the credential), so each IP gets a budget; a guessing loop does not fit.
// The rejection (429) is the sign-in module's global one.
public sealed class DownloadServices : IServiceModule
{
    public const string RateLimit = "downloads";
    public const string Path = "/api/downloads";
    public const int RequestsPerMinutePerIp = 60;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddRateLimiter(options => options.AddPolicy(RateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey.For(context.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinutePerIp, Window = TimeSpan.FromMinutes(1) })));
}
