using System.Net;
using System.Text;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;

namespace ShipLean.WebApi.Tests.WorkItemDetails.Attachments;

// SVG uploads are stored sanitised, and images are served inline (an SVG under a sandbox policy) while other files stay downloads.
[TestFixture]
public class AttachmentSvgApiTests : ApiFixture
{
    private static readonly string ItemUrl = $"api/work-items/{SeedIds.WorkItems.Ship90}/attachments";

    private const string HostileSvg =
        """
        <svg xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink" onload="alert(1)">
          <script>alert(2)</script>
          <foreignObject><div>hi</div></foreignObject>
          <rect width="10" height="10" fill="red" onclick="alert(3)"/>
          <image xlink:href="https://evil.example/x.png"/>
          <a href="javascript:alert(4)"><circle id="c" r="5" style="fill:url(https://evil.example/f)"/></a>
          <use href="#c"/>
        </svg>
        """;

    [Test]
    public async Task Upload_WhenSvgHasScript_ShouldStoreSanitised()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "logo.svg", "image/svg+xml", Encoding.UTF8.GetBytes(HostileSvg));

        using var download = await Api.GetAsync(added.DownloadUrl);
        var stored = await download.Content.ReadAsStringAsync();

        stored.ShouldContain("<rect");
        stored.ShouldContain("href=\"#c\"");
        foreach (var gone in new[] { "script", "alert", "foreignObject", "onload", "onclick", "evil.example", "javascript:" })
        {
            stored.ShouldNotContain(gone);
        }
    }

    [Test]
    public async Task Upload_WhenSvgIsDeclaredAsHtml_ShouldStoreAsSvgImage()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "sneaky.svg", "text/html", Encoding.UTF8.GetBytes(HostileSvg));

        added.ContentType.ShouldBe("image/svg+xml");
    }

    [Test]
    public async Task Upload_WhenSvgIsNotXml_ShouldStoreAsOpaqueDownload()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "broken.svg", "image/svg+xml", Encoding.UTF8.GetBytes("<svg><oops>"));

        using var download = await Api.GetAsync(added.DownloadUrl);

        added.ContentType.ShouldBe("application/octet-stream");
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Test]
    public async Task Content_WhenImage_ShouldServeInline()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "pixel.png", "image/png", [137, 80, 78, 71]);

        using var download = await Api.GetAsync(added.DownloadUrl);

        download.StatusCode.ShouldBe(HttpStatusCode.OK);
        download.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        download.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }

    [Test]
    public async Task Content_WhenImageNameButNotImageType_ShouldStayADownload()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "fake.png", "text/html", Encoding.UTF8.GetBytes("<script>alert(1)</script>"));

        using var download = await Api.GetAsync(added.DownloadUrl);

        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("attachment");
    }

    [Test]
    public async Task Content_WhenSvg_ShouldSendSandboxCsp()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "icon.svg", "image/svg+xml", Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/2000/svg\"/>"));

        using var download = await Api.GetAsync(added.DownloadUrl);

        download.Content.Headers.ContentType!.MediaType.ShouldBe("image/svg+xml");
        download.Content.Headers.ContentDisposition!.DispositionType.ShouldBe("inline");
        download.Headers.GetValues("Content-Security-Policy").Single().ShouldContain("default-src 'none'");
    }

    [Test]
    public async Task Upload_WhenSvgIsNestedTooDeep_ShouldStoreAsOpaqueDownloadAndKeepServing()
    {
        var deep = $"<svg xmlns=\"http://www.w3.org/2000/svg\">{string.Concat(Enumerable.Repeat("<g>", 50_000))}{string.Concat(Enumerable.Repeat("</g>", 50_000))}</svg>";

        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "deep.svg", "image/svg+xml", Encoding.UTF8.GetBytes(deep));

        added.ContentType.ShouldBe("application/octet-stream");
        (await Api.GetAsync(added.DownloadUrl)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_WhenSvgRootIsNotInTheSvgNamespace_ShouldStoreAsOpaqueDownload()
    {
        var added = await AttachmentUploads.UploadAsync(Client, ItemUrl, "xhtml.svg", "image/svg+xml", Encoding.UTF8.GetBytes("<svg xmlns=\"http://www.w3.org/1999/xhtml\"><script>alert(1)</script></svg>"));

        added.ContentType.ShouldBe("application/octet-stream");
    }
}
