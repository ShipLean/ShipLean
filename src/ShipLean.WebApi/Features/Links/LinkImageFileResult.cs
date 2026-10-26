using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ShipLean.WebApi.Features.Links;

// A link card's stored preview image or favicon: the stored type, never sniffed by the browser, a sandbox policy (a stored SVG
// can't run script even when opened directly), same-origin only, and revalidated on every use, since a refresh replaces it
// behind the same address.
internal sealed class LinkImageFileResult(Stream content, string contentType) : FileStreamResult(content, contentType)
{
    public override Task ExecuteResultAsync(ActionContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = "sandbox";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers.CacheControl = "private, no-cache";
        headers.Vary = HeaderNames.Cookie;
        return base.ExecuteResultAsync(context);
    }
}
