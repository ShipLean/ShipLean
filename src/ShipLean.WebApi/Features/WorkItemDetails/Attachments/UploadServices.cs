using System.Threading.RateLimiting;
using ShipLean.Contracts;
using ShipLean.WebApi.Infrastructure;

namespace ShipLean.WebApi.Features.WorkItemDetails.Attachments;

// The upload endpoint is anonymous (the URL's token is the credential), so each IP gets a budget: an agent uploads a handful
// of files a minute; a guessing loop does not fit. The rejection (429) is the sign-in module's global one.
public sealed class UploadServices : IServiceModule
{
    public const string RateLimit = "uploads";
    public const string Path = "/api/uploads";
    public const int RequestsPerMinutePerIp = 60;

    public void AddServices(IServiceCollection services, IConfiguration configuration)
        => services.AddRateLimiter(options => options.AddPolicy(RateLimit, context => RateLimitPartition.GetFixedWindowLimiter(
            ClientIpKey.For(context.Connection.RemoteIpAddress),
            _ => new FixedWindowRateLimiterOptions { PermitLimit = RequestsPerMinutePerIp, Window = TimeSpan.FromMinutes(1) })));
}
