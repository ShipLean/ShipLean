using Microsoft.AspNetCore.Http;
using ShipLean.Contracts;

namespace ShipLean.Application.Requests;

// The ClientHeader of the current request. Blank counts as absent. Takes the request as a function, so it needs nothing
// but the request itself (RequestOriginServices wires it to IHttpContextAccessor).
public sealed class RequestOrigin(Func<HttpContext?> currentRequest) : IRequestOrigin
{
    public string? ClientId
    {
        get
        {
            var value = currentRequest()?.Request.Headers[ClientHeader.Name].FirstOrDefault();
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
