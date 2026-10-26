using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ShipLean.WebApi.Features.Profile;

// A profile photo: the type the upload's bytes proved, never sniffed by the browser, a sandbox policy, inline disposition, same-origin only, and caching keyed on
// ?v= (the photo's version): a request for the current version may be kept for a year, anything else must be revalidated.
internal sealed class PhotoFileResult(Stream content, string contentType, bool versionIsCurrent) : FileStreamResult(content, contentType)
{
    public override Task ExecuteResultAsync(ActionContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = "sandbox";
        headers.ContentDisposition = "inline; filename=\"photo\"";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers.CacheControl = versionIsCurrent ? "private, max-age=31536000, immutable" : "private, no-cache";
        headers.Vary = HeaderNames.Cookie;
        return base.ExecuteResultAsync(context);
    }
}
