using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;

namespace ShipLean.WebApi.Features.WorkItemDetails.Attachments;

// The stored content type, no sniffing, and a sandbox policy for any client that renders it anyway. A download is always an
// attachment (never rendered in our origin); only an image (a stored image type) is served inline, so <img> and a direct
// visit show it, and an SVG there still runs under the sandbox.
internal sealed class AttachmentFileResult(Stream content, string contentType, string name, bool inline) : FileStreamResult(content, contentType)
{
    // An SVG may load nothing from elsewhere whatever the sanitiser missed (only its own styles and data: images).
    private const string SvgPolicy = "sandbox; default-src 'none'; style-src 'unsafe-inline'; img-src data:";

    public override Task ExecuteResultAsync(ActionContext context)
    {
        var headers = context.HttpContext.Response.Headers;
        if (inline)
        {
            var disposition = new ContentDispositionHeaderValue("inline");
            disposition.SetHttpFileName(name);
            headers.ContentDisposition = disposition.ToString();
        }
        else
        {
            FileDownloadName = name;
        }

        headers.XContentTypeOptions = "nosniff";
        headers.ContentSecurityPolicy = ContentType.StartsWith("image/svg+xml", StringComparison.OrdinalIgnoreCase) ? SvgPolicy : "sandbox";
        return base.ExecuteResultAsync(context);
    }
}
