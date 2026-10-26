using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Identity;
using ShipLean.Contracts;

namespace ShipLean.TestSupport.Hosting;

// The host's ICurrentUser in tests: the request's user as in production, and, for a service a test calls straight from
// Host.Services (no request), the user the fixture named (Host.ActAs).
public sealed class ActingCurrentUser(IServiceProvider services, CurrentUser real) : ICurrentUser
{
    public Guid? Acting { get; set; }

    public Guid Id => services.GetService<IHttpContextAccessor>()?.HttpContext is null && Acting is { } id ? id : real.Id;
}
