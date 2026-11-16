using System.Security.Claims;
using Agoda.IoC.Core;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;
using ShipLean.Contracts;

namespace ShipLean.WebApi.Infrastructure;

// The one class that touches IHttpContextAccessor (AG0009); see ICurrentRequest. CurrentRequestServices adds the accessor.
// AG0009 refuses the accessor as any parameter, this constructor's included, so it is resolved from the container instead.
[RegisterSingleton(For = typeof(ICurrentRequest))]
public sealed class CurrentRequest(IServiceProvider services, IOptions<ForwardedHeadersOptions> forwarding) : ICurrentRequest
{
    private const string NoRequest = "No request to sign in on.";

    private IHttpContextAccessor? _accessor;

    public bool Exists => Accessor.HttpContext is not null;

    public ClaimsPrincipal? User => Accessor.HttpContext?.User;

    public PathString Path => Accessor.HttpContext?.Request.Path ?? PathString.Empty;

    public Endpoint? Endpoint => Accessor.HttpContext?.GetEndpoint();

    public IDictionary<object, object?>? Items => Accessor.HttpContext?.Items;

    public string Origin => RequestOrigin.Of(Request, forwarding.Value);

    public string? OriginClusterFallback => RequestOrigin.ClusterFallback(Request, forwarding.Value);

    public string? Header(string name) => Accessor.HttpContext?.Request.Headers[name].FirstOrDefault();

    public Task SignInAsync(string scheme, ClaimsPrincipal principal, AuthenticationProperties properties)
        => Context.SignInAsync(scheme, principal, properties);

    public Task SignOutAsync(string scheme) => Context.SignOutAsync(scheme);

    public IDisposable ActAs(ClaimsPrincipal user)
    {
        var previous = Accessor.HttpContext;
        Accessor.HttpContext = new DefaultHttpContext { User = user };
        return new Restore(() => Accessor.HttpContext = previous);
    }

    private IHttpContextAccessor Accessor => _accessor ??= services.GetRequiredService<IHttpContextAccessor>();

    private HttpContext Context => Accessor.HttpContext ?? throw new InvalidOperationException(NoRequest);

    private HttpRequest Request => Accessor.HttpContext?.Request ?? throw new InvalidOperationException("There is no request to take an origin from.");

    private sealed class Restore(Action restore) : IDisposable
    {
        public void Dispose() => restore();
    }
}
