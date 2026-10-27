using Microsoft.Net.Http.Headers;
using ShipLean.Application.Identity;
using ShipLean.Contracts;
using ShipLean.Contracts.Errors;
using ShipLean.WebApi.Features.WorkItemDetails.Attachments;

namespace ShipLean.WebApi.Features.Auth;

// CSRF: the cookie is SameSite=Lax, and a cookie-carrying write to /api must also be something a cross-site HTML form
// cannot send: a JSON body, or the X-ShipLean-Client header the SPA's fetch wrapper puts on every request (a form can
// set neither). An agent's upload URL is exempt: its token is the credential, not ambient. Uploads and bodiless POSTs rely on the header. In password mode a request without the cookie carries no
// ambient credentials, so there is nothing to forge; in proxy mode the proxy's session is one, so every write is checked.
public static class CrossSiteWriteGuard
{
    public static void Check(HttpRequest request)
    {
        if (HttpMethods.IsGet(request.Method) || HttpMethods.IsHead(request.Method) || HttpMethods.IsOptions(request.Method)
            || !request.Path.StartsWithSegments("/api")
            || request.Path.StartsWithSegments(UploadServices.Path)
            || (!request.Cookies.ContainsKey(AuthServices.CookieName) && !ProxyMode(request))
            || request.Headers.ContainsKey(ClientHeader.Name)
            || IsJson(request.ContentType))
        {
            return;
        }
        throw ApiException.Forbidden($"This request was refused: a write must send JSON or the {ClientHeader.Name} header.");
    }

    // In proxy mode the proxy's own session is an ambient credential the browser attaches to a forged form post, with or
    // without ShipLean's cookie, so every write is guarded.
    private static bool ProxyMode(HttpRequest request)
        => request.HttpContext.RequestServices.GetRequiredService<ProxySignInSettings>().IsProxy;

    private static bool IsJson(string? contentType)
        => MediaTypeHeaderValue.TryParse(contentType, out var media)
           && string.Equals(media.MediaType.Value, "application/json", StringComparison.OrdinalIgnoreCase);
}
