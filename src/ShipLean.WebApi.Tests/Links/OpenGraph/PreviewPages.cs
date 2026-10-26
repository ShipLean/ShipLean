using ShipLean.TestSupport.Http;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// What the stub server answers for a page with Open Graph tags, its image and its favicon.
public static class PreviewPages
{
    // A 1x1 PNG and the smallest well-formed ICO header: real magic bytes, which is what the source checks.
    public static byte[] Png => Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");
    public static byte[] Ico => [0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x10, 0x10, 0x00, 0x00, 0x01, 0x00, 0x20, 0x00];

    public static string Article(string title, string image = "/og.png", string icon = "/icons/site.ico")
        => $"""
           <!doctype html>
           <html><head>
           <title>Fallback title</title>
           <meta property="og:title" content="{title}">
           <meta property="og:description" content="  Well-designed forms
             help users convert.  ">
           <meta property="og:site_name" content="web.dev">
           <meta property="og:image" content="{image}">
           <link rel="shortcut icon" href="{icon}">
           </head><body><p>Body</p></body></html>
           """;

    public static void ServeArticle(StubHttpServer server, string path, string title)
    {
        server.Respond(path, Article(title));
        server.Respond("/og.png", Png, "image/png");
        server.Respond("/icons/site.ico", Ico, "image/vnd.microsoft.icon");
    }
}
