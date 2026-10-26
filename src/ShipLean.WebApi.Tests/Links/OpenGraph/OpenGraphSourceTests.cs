using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.Links.OpenGraph;
using ShipLean.Application.Http;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Links;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Http;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// The Open Graph source as the app registers it (one of the ILinkCardSources), reading pages from a stub server on loopback.
// Loopback is let through; "private.test" resolves to a private address and meets the real rules. Seeded, so a card can be
// added to SHIP-101 through the API.
[TestFixture]
public class OpenGraphSourceTests : ApiFixture
{
    private static readonly JsonSerializerOptions Wire = new(JsonSerializerDefaults.Web) { Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };

    private StubHttpServer _server = null!;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        services.AddSingleton<IAddressCheck, LoopbackAllowedAddressCheck>();
        services.AddSingleton<IHostResolver>(new FixedHostResolver(new Dictionary<string, string> { ["private.test"] = "192.168.1.20" }));
    };

    [OneTimeSetUp]
    public async Task StartStubServer()
    {
        _server = await StubHttpServer.StartAsync();
    }

    [OneTimeTearDown]
    public async Task StopStubServer()
    {
        await _server.DisposeAsync();
    }

    private async Task<LinkCardFetch> FetchAsync(Uri address)
    {
        using var scope = Host.Services.CreateScope();
        var source = scope.ServiceProvider.GetServices<ILinkCardSource>().OfType<OpenGraphSource>().Single();
        return await source.FetchAsync(address, CancellationToken.None);
    }

    private async Task<(byte[] Bytes, string ContentType)> BlobAsync(string key)
    {
        var blob = await Host.Services.GetRequiredService<IBlobStore>().GetAsync(key, CancellationToken.None);
        blob.ShouldNotBeNull();
        await using var content = blob.Content;
        using var bytes = new MemoryStream();
        await content.CopyToAsync(bytes);
        return (bytes.ToArray(), blob.ContentType);
    }

    [Test]
    public async Task OpenGraph_WhenPageHasTags_ShouldReturnTitleDescriptionImage()
    {
        PreviewPages.ServeArticle(_server, "/articles/forms", "Payment and address form best practices");

        var fetch = await FetchAsync(_server.UrlOf("/articles/forms"));

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        fetch.Kind.ShouldBe(LinkKind.Page);
        fetch.Title.ShouldBe("Payment and address form best practices");
        fetch.Description.ShouldBe("Well-designed forms help users convert.");
        fetch.Site.ShouldBe("web.dev");
        var image = await BlobAsync(fetch.ImageKey!);
        image.Bytes.ShouldBe(PreviewPages.Png);
        image.ContentType.ShouldBe("image/png");
        var favicon = await BlobAsync(fetch.FaviconKey!);
        favicon.Bytes.ShouldBe(PreviewPages.Ico);
        favicon.ContentType.ShouldBe("image/x-icon");
    }

    [Test]
    public async Task OpenGraph_WhenHostPrivate_ShouldReturnPrivateStatus()
    {
        var fetch = await FetchAsync(new Uri("https://private.test/dashboards/checkout"));

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Private);
        fetch.Title.ShouldBeNull();
        fetch.ImageKey.ShouldBeNull();
    }

    [Test]
    public async Task OpenGraph_WhenPageHasNoOpenGraphTags_ShouldFallBackToTitleAndDescriptionMeta()
    {
        _server.Respond("/plain-page", """<html><head><title> Plain page </title><meta name="description" content="Just a page."></head></html>""");

        var fetch = await FetchAsync(_server.UrlOf("/plain-page"));

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        fetch.Title.ShouldBe("Plain page");
        fetch.Description.ShouldBe("Just a page.");
        fetch.Site.ShouldBeNull();
        fetch.ImageKey.ShouldBeNull();
    }

    [Test]
    public async Task OpenGraph_WhenPageHasNoIconLink_ShouldStoreTheSiteFavicon()
    {
        _server.Respond("/no-icon-link", "<title>No icon link</title>");
        _server.Respond("/favicon.ico", PreviewPages.Ico, "image/x-icon");

        var fetch = await FetchAsync(_server.UrlOf("/no-icon-link"));

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        (await BlobAsync(fetch.FaviconKey!)).Bytes.ShouldBe(PreviewPages.Ico);
    }

    [Test]
    public async Task AddUrl_WhenPageHasASiteName_ShouldShowItWhereTheCardNamesItsSite()
    {
        PreviewPages.ServeArticle(_server, "/articles/site-name", "Named site");
        var url = _server.UrlOf("/articles/site-name").AbsoluteUri;
        var item = $"api/work-items/{SeedIds.WorkItems.Ship101}";

        using var previewResponse = await Api.PostJsonAsync($"{item}/link-cards/preview", new { url });
        using var addResponse = await Api.PostJsonAsync($"{item}/link-cards", new { url });

        (await previewResponse.Content.ReadFromJsonAsync<LinkPreviewDto>(Wire))!.Host.ShouldBe("web.dev");
        addResponse.StatusCode.ShouldBe(HttpStatusCode.Created);
        var added = (await addResponse.Content.ReadFromJsonAsync<LinkCardDto>(Wire))!;
        added.Host.ShouldBe("web.dev");
        added.Title.ShouldBe("Named site");
        added.ImageUrl.ShouldBe($"/api/link-cards/{added.Id}/image");
    }

    [Test]
    public async Task OpenGraph_WhenImageIsSvgOrMislabelled_ShouldStoreNoImage()
    {
        _server.Respond("/svg-page", PreviewPages.Article("Vector", image: "/og.svg", icon: "/fake.png"));
        _server.Respond("/og.svg", """<svg xmlns="http://www.w3.org/2000/svg"><script>alert(1)</script></svg>""", "image/svg+xml");
        _server.Respond("/fake.png", "<html>not a png</html>", "image/png");

        var fetch = await FetchAsync(_server.UrlOf("/svg-page"));

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        fetch.Title.ShouldBe("Vector");
        fetch.ImageKey.ShouldBeNull();
        fetch.FaviconKey.ShouldBeNull();
    }

    [Test]
    public async Task OpenGraph_WhenImageIsOver1Mb_ShouldStoreNoImage()
    {
        var huge = new byte[(1024 * 1024) + 1];
        PreviewPages.Png.CopyTo(huge, 0);
        _server.Respond("/huge-image-page", PreviewPages.Article("Huge", image: "/huge.png"));
        _server.Respond("/huge.png", huge, "image/png");

        var fetch = await FetchAsync(_server.UrlOf("/huge-image-page"));

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        fetch.ImageKey.ShouldBeNull();
    }

    [Test]
    public async Task OpenGraph_WhenImagePointsAtAPrivateHost_ShouldStoreNoImageAndStillBeOk()
    {
        _server.Respond("/private-image-page", PreviewPages.Article("Private image", image: "http://private.test/og.png"));

        var fetch = await FetchAsync(_server.UrlOf("/private-image-page"));

        fetch.PreviewStatus.ShouldBe(LinkPreviewStatus.Ok);
        fetch.ImageKey.ShouldBeNull();
    }

    [Test]
    public async Task OpenGraph_WhenTitleIsHuge_ShouldCapIt()
    {
        _server.Respond("/long-title", $"<title>{new string('x', 10_000)}</title>");

        var fetch = await FetchAsync(_server.UrlOf("/long-title"));

        fetch.Title!.Length.ShouldBe(OpenGraphPage.MaxTitle + 1);
    }

    [Test]
    public async Task CanHandle_WhenAddressIsHttpOrHttps_ShouldBeTrueAndOtherwiseFalse()
    {
        using var scope = Host.Services.CreateScope();
        var source = scope.ServiceProvider.GetServices<ILinkCardSource>().OfType<OpenGraphSource>().Single();

        source.Order.ShouldBe(100);
        (await source.CanHandleAsync(new Uri("https://web.dev/articles/forms"), CancellationToken.None)).ShouldBeTrue();
        (await source.CanHandleAsync(new Uri("http://example.org/"), CancellationToken.None)).ShouldBeTrue();
        (await source.CanHandleAsync(new Uri("ftp://files.example/readme"), CancellationToken.None)).ShouldBeFalse();
        (await source.CanHandleAsync(new Uri("file:///etc/passwd"), CancellationToken.None)).ShouldBeFalse();
    }
}
