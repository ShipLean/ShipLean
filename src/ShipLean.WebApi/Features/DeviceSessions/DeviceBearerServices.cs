using Microsoft.AspNetCore.Authentication;
using ShipLean.Contracts;
using ShipLean.WebApi.Features.Auth;

namespace ShipLean.WebApi.Features.DeviceSessions;

// The DeviceBearer scheme. Its challenge and forbid go to the cookie scheme, so CookieSessionEvents writes the 401
// (session_ended once the handler marked the session ended) and the 403, as for a cookie.
public sealed class DeviceBearerServices : IServiceModule
{
    public void AddServices(IServiceCollection services, IConfiguration configuration)
    {
        services.AddAuthentication()
            .AddScheme<AuthenticationSchemeOptions, DeviceBearerHandler>(DeviceBearerHandler.Scheme, options =>
            {
                options.ForwardChallenge = AuthServices.CookieScheme;
                options.ForwardForbid = AuthServices.CookieScheme;
            });
    }
}
