using System.Net;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Fakes.Blobs;
using ShipLean.Application.Features.Links.Rows;
using ShipLean.Contracts.Links;
using ShipLean.Contracts.Blobs;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Blobs;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using Shouldly;

namespace ShipLean.WebApi.Tests.Links.OpenGraph;

// GET api/link-cards/{id}/image and /favicon over the seed: SHIP-101's pull request link has both stored, its page link has neither.
[TestFixture]
public class LinkCardImagesTests : ApiFixture
{
    private static readonly string PullLink = $"api/link-cards/{SeedIds.Links.Ship101Pull}";

    // Seeded links are rows only; their preview bytes go into an in-memory blob store.
    protected override Action<IServiceCollection>? ConfigureTestServices => services => services.AddSingleton<IBlobStore, InMemoryBlobStore>();

    [OneTimeSetUp]
    public Task PutSeedAttachmentBlobs() => SeedAttachmentBlobs.PutAllAsync(Host.Services.GetRequiredService<IBlobStore>());

    [TestCase("image", "image/png")]
    [TestCase("favicon", "image/x-icon")]
    public async Task LinkCardImage_WhenMember_ShouldServeTheStoredBytesUnsniffedAndSandboxed(string which, string contentType)
    {
        using var response = await Api.GetAsync($"{PullLink}/{which}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType!.MediaType.ShouldBe(contentType);
        response.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldStartWith("sandbox");
        (await response.Content.ReadAsByteArrayAsync()).ShouldNotBeEmpty();
    }

    [TestCase("image")]
    [TestCase("favicon")]
    public async Task LinkCardImage_WhenNonMember_ShouldReturnNotFound(string which)
    {
        using var response = await Api.ActingAs(TestUser.Kai).GetAsync($"{PullLink}/{which}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase("image")]
    [TestCase("favicon")]
    public async Task LinkCardImage_WhenTheCardHasNone_ShouldReturnNotFound(string which)
    {
        using var response = await Api.GetAsync($"api/link-cards/{SeedIds.Links.Ship101PageToArchive}/{which}");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [TestCase("text/html")]
    [TestCase("image/svg+xml")]
    [TestCase("application/octet-stream")]
    public async Task LinkCardImage_WhenTheStoredBlobIsNotARasterImage_ShouldReturnNotFound(string contentType)
    {
        var key = $"link-previews/test-{Guid.NewGuid():N}";
        await using (var bytes = new MemoryStream("<script>alert(1)</script>"u8.ToArray()))
        {
            await Host.Services.GetRequiredService<IBlobStore>().PutAsync(key, bytes, contentType, CancellationToken.None);
        }
        Guid linkId;
        using (var scope = Host.Services.CreateScope())
        {
            var rows = scope.ServiceProvider.GetRequiredService<LinkCardRows>();
            var now = TimeProvider.System.GetUtcNow();
            linkId = await rows.InsertUrlAsync(SeedIds.WorkItems.Ship101, $"https://docs.example/{Guid.NewGuid():N}", null, null, LinkKind.Page, SeedIds.Users.Joel, now, CancellationToken.None);
            await rows.UpdatePreviewAsync(linkId, new LinkPreviewWrite("Stored", null, null, key, key, LinkPreviewStatus.Ok, now, null, null, null), CancellationToken.None);
        }

        foreach (var which in new[] { "image", "favicon" })
        {
            using var response = await Api.GetAsync($"api/link-cards/{linkId}/{which}");

            (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
        }
    }

    [Test]
    public async Task LinkCardImage_WhenLinkIsUnknown_ShouldReturnNotFound()
    {
        using var response = await Api.GetAsync($"api/link-cards/{Guid.NewGuid()}/image");

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
