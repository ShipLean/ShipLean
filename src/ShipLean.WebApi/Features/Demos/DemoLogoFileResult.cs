using Microsoft.AspNetCore.Mvc;

namespace ShipLean.WebApi.Features.Demos;

// A project's demo logo: the stored type (the bytes proved it), never sniffed by the browser, and the policy an attachment SVG gets (sandboxed, no outside loads).
// Inline for the settings preview's <img>, an attachment for the agent's download. Same-origin only; private. A request for the logo's current ?v= version may be kept for a year, anything else must be revalidated (as PhotoFileResult does).
internal sealed class DemoLogoFileResult(Stream content, string contentType, bool inline, bool versionIsCurrent = false) : FileStreamResult(content, contentType)
{
    private const string Policy = "sandbox; default-src 'none'; style-src 'unsafe-inline'; img-src data:";

    private static string PolicyOf() => Policy;

    public override Task ExecuteResultAsync(ActionContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = PolicyOf();
        headers.ContentDisposition = inline ? "inline; filename=\"logo\"" : "attachment; filename=\"logo\"";
        headers["Cross-Origin-Resource-Policy"] = "same-origin";
        headers.CacheControl = versionIsCurrent ? "private, max-age=31536000, immutable" : "private, no-cache";
        return base.ExecuteResultAsync(context);
    }
}
