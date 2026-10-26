namespace ShipLean.WebApi.Features.Agents.Authorize;

// X-Frame-Options: DENY and CSP frame-ancestors 'none' on every response, so a sibling site cannot frame the consent page
// (RFC 6749 10.13, RFC 9700). First in the pipeline and set as the response starts, so it also covers static files, the
// SPA shell and error bodies. A CSP header set elsewhere keeps its directives and gets this one appended.
public sealed class AntiFramingStartupFilter : IStartupFilter
{
    public const string FrameOptions = "X-Frame-Options";
    public const string ContentSecurityPolicy = "Content-Security-Policy";
    public const string FrameAncestorsNone = "frame-ancestors 'none'";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use((context, nextMiddleware) =>
        {
            context.Response.OnStarting(() =>
            {
                var headers = context.Response.Headers;
                headers[FrameOptions] = "DENY";
                var existing = headers[ContentSecurityPolicy].ToString();
                if (!existing.Contains("frame-ancestors", StringComparison.OrdinalIgnoreCase))
                {
                    headers[ContentSecurityPolicy] = existing.Length == 0 ? FrameAncestorsNone : $"{existing}; {FrameAncestorsNone}";
                }
                return Task.CompletedTask;
            });
            return nextMiddleware();
        });
        next(app);
    };
}
