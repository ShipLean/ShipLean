using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;

namespace ShipLean.Contracts;

// The current HTTP request, and the only way to reach it (AG0009): inject this instead of IHttpContextAccessor, HttpContext
// or a Func<HttpContext?>. It exposes only what callers need; if it lacks something, add a member here rather than read the
// context yourself. Every member reads the request at call time, so a singleton can hold it. Outside a request Exists is
// false, the readers return null (or empty) and the members that act on a request throw.
// A SignalR hub method takes its user from the hub's Context.User, not from here (docs/events.md).
public interface ICurrentRequest
{
    // True inside a request (or inside an ActAs).
    bool Exists { get; }

    ClaimsPrincipal? User { get; }

    // The request path, PathString.Empty outside a request.
    PathString Path { get; }

    // The endpoint routing matched, or null.
    Endpoint? Endpoint { get; }

    // The request's per-request items, or null outside a request. Lock on it when more than one thread may write.
    IDictionary<object, object?>? Items { get; }

    // The origin to publish in a URL a caller opens from outside the cluster (forwarded host and scheme from a trusted proxy,
    // else the request's own). Throws outside a request.
    string Origin { get; }

    // Why Origin fell back to a cluster service name, or null when it is one a laptop can open. Throws outside a request.
    string? OriginClusterFallback { get; }

    // The first value of a request header, or null when absent or outside a request.
    string? Header(string name);

    // Issue or clear an authentication cookie on this request's response. Throw outside a request.
    Task SignInAsync(string scheme, ClaimsPrincipal principal, AuthenticationProperties properties);

    Task SignOutAsync(string scheme);

    // Background work that must run as a user (code that reads the user from the request): until disposed, the current async
    // flow sees a request carrying only that user. Disposing puts back whatever request was there before.
    IDisposable ActAs(ClaimsPrincipal user);
}
