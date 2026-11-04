using System.Buffers.Binary;
using System.Net;
using System.Text;
using System.Text.Json;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Demos;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;

namespace ShipLean.WebApi.Tests.Demos;

// K18's logo, real, over the real routes and a real blob store: a project's logo is untrusted input. The bytes decide what it is, an SVG is stored
// cleaned, and the agent's link reads the project's current logo through a live token and a current membership.
[TestFixture]
public class DemoLogoApiTests : ApiFixture
{
    private static readonly string LogoPath = $"api/projects/{SeedIds.Projects.Ship}/demo-brand/logo";
    private const string SvgNs = "http://www.w3.org/2000/svg";

    private static HttpRequestMessage Upload(HttpMethod method, byte[] bytes, string fileName, Guid? actingAs = null)
    {
        var request = new HttpRequestMessage(method, LogoPath) { Content = new MultipartFormDataContent { { new ByteArrayContent(bytes), "file", fileName } } };
        request.Headers.Add(ClientHeader.Name, ApiClient.ClientId);
        if (actingAs is { } user)
        {
            request.Headers.Add(TestAuthentication.UserHeader, user.ToString());
        }

        return request;
    }

    private Task<HttpResponseMessage> PutAsync(byte[] bytes, string fileName = "logo.png", Guid? actingAs = null)
        => Api.Http.SendAsync(Upload(HttpMethod.Put, bytes, fileName, actingAs));

    // A PNG header that claims the size given: all the format check reads.
    private static byte[] Png(uint width, uint height, int length = 64)
    {
        var bytes = new byte[length];
        new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }.CopyTo(bytes, 0);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(8), 13);
        "IHDR"u8.CopyTo(bytes.AsSpan(12));
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(16), width);
        BinaryPrimitives.WriteUInt32BigEndian(bytes.AsSpan(20), height);
        return bytes;
    }

    private static byte[] Svg(string inner, string rootAttributes = "") => Encoding.UTF8.GetBytes($"<svg xmlns=\"{SvgNs}\" {rootAttributes} viewBox=\"0 0 10 10\">{inner}</svg>");

    private static Task<ApiError> RefusalOfAsync(HttpResponseMessage response) => ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest);

    private async Task<string> StoredBlobKeyAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<string>("SELECT logo_blob FROM project_demo_settings WHERE project_id = @id", new { id = SeedIds.Projects.Ship });
    }

    private async Task<(string? Blob, int Version)> RowAsync()
    {
        await using var database = await OpenDatabaseAsync();
        return await database.QuerySingleAsync<(string? Blob, int Version)>(
            "SELECT logo_blob, logo_version FROM project_demo_settings WHERE project_id = @id", new { id = SeedIds.Projects.Ship });
    }

    // A BAU item in SHIP and a live logo token for it, as K6's decision leaves them.
    private async Task<(Guid ItemId, string Token)> NewTokenAsync(Guid userId, TimeSpan? lifetime = null)
    {
        await using var database = await OpenDatabaseAsync();
        var item = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("bau").WithTitle("Sprint demo").InsertAsync(database);
        await using var scope = Host.Services.CreateAsyncScope();
        var issued = await scope.ServiceProvider.GetRequiredService<ITransferTokens>().IssueAsync(
            new NewTransfer(TransferKind.DemoLogo, item, null, "logo", "image/png", userId, null, "Test Agent (unverified)", lifetime ?? TimeSpan.FromHours(24)));
        return (item, issued.Token);
    }

    private Task<HttpResponseMessage> DownloadAsync(string token)
        => Api.ActingAs(TestUser.Anonymous).GetAsync($"api/downloads/demo-logo?token={Uri.EscapeDataString(token)}");

    private static Task<ApiError> NotFoundOfAsync(HttpResponseMessage response) => ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);

    private const string NotValid = "That download link is not valid.";

    [Test]
    public async Task PutLogo_WhenPng_ShouldStoreAndServeIt()
    {
        var png = Png(200, 100);

        using var put = await PutAsync(png);

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        var brand = JsonDocument.Parse(await put.Content.ReadAsStringAsync()).RootElement;
        brand.GetProperty("logoContentType").GetString().ShouldBe("image/png");
        var version = (await RowAsync()).Version;
        brand.GetProperty("logoUrl").GetString().ShouldBe($"/api/projects/{SeedIds.Projects.Ship}/demo-brand/logo?v={version}");
        using var served = await Api.GetAsync($"{LogoPath}?v={version}");
        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await served.Content.ReadAsByteArrayAsync()).ShouldBe(png);
        (await StoredBlobKeyAsync()).ShouldStartWith($"demo-logos/{SeedIds.Projects.Ship:N}/");
    }

    [Test]
    public async Task PutLogo_WhenSvgWithScript_ShouldStoreTheCleanedBytes()
    {
        var dirty = Svg(
            "<script>alert(1)</script><image href=\"http://evil.example/x.png\"/><rect width=\"4\" height=\"4\" fill=\"#c00\" onclick=\"steal()\"/><use href=\"#x\" xml:base=\"https://evil.example/x.svg\"/><g xml:BASE=\"https://evil.example/y.svg\"/>",
            "onload=\"alert(2)\" xml:base=\"https://evil.example/root.svg\"");

        using var put = await PutAsync(dirty, "logo.svg");

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var served = await Api.GetAsync(LogoPath);
        served.Content.Headers.ContentType!.MediaType.ShouldBe("image/svg+xml");
        var stored = await served.Content.ReadAsStringAsync();
        stored.ShouldNotContain("<script");
        stored.ShouldNotContain("onload");
        stored.ShouldNotContain("onclick");
        stored.ShouldNotContain("evil.example");
        stored.ShouldNotContain("base", Case.Insensitive);
        stored.ShouldContain("<rect");
        stored.ShouldContain("#c00");
    }

    [TestCase("<rect width=\"4\" height=\"4\" style=\"background:image(&quot;https://evil.example/x&quot;)\"/>")]
    [TestCase("<rect width=\"4\" height=\"4\" style=\"background:-webkit-image-set(&quot;https://evil.example/x&quot; 1x)\"/>")]
    [TestCase("<rect width=\"4\" height=\"4\" style=\"background:cross-fade(&quot;https://evil.example/x&quot;,50%)\"/>")]
    [TestCase("<rect width=\"4\" height=\"4\" style=\"background:element(#a)\"/>")]
    [TestCase("<rect width=\"4\" height=\"4\" style=\"background:paint(&quot;https://evil.example/x&quot;)\"/>")]
    [TestCase("<rect width=\"4\" height=\"4\" style=\"cursor:https://evil.example/x\"/>")]
    [TestCase("<style>rect { background: image(\"https://evil.example/x\") }</style><rect width=\"4\" height=\"4\"/>")]
    [TestCase("<style>rect { background: -webkit-image-set(\"https://evil.example/x\" 1x) }</style><rect width=\"4\" height=\"4\"/>")]
    [TestCase("<style>rect { background: cross-fade(\"https://evil.example/x\",50%) }</style><rect width=\"4\" height=\"4\"/>")]
    [TestCase("<style>rect { background: element(#a) }</style><rect width=\"4\" height=\"4\"/>")]
    [TestCase("<style>rect { background: paint(\"https://evil.example/x\") }</style><rect width=\"4\" height=\"4\"/>")]
    [TestCase("<style>rect { content: \"https://evil.example/x\" }</style><rect width=\"4\" height=\"4\"/>")]
    public async Task PutLogo_WhenSvgWithCssResourceFunction_ShouldStoreWithoutIt(string inner)
    {
        using var put = await PutAsync(Svg(inner + "<text style=\"font-family:&quot;Inter&quot;\">a</text><rect fill=\"url(#g)\" width=\"1\" height=\"1\"/>"), "logo.svg");

        put.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var served = await Api.GetAsync(LogoPath);
        var stored = await served.Content.ReadAsStringAsync();
        stored.ShouldNotContain("evil.example");
        stored.ShouldNotContain("image(");
        stored.ShouldNotContain("image-set(");
        stored.ShouldNotContain("cross-fade(");
        stored.ShouldNotContain("element(");
        stored.ShouldNotContain("paint(");
        stored.ShouldContain("font-family:&quot;Inter&quot;");
        stored.ShouldContain("url(#g)");
    }

    [Test]
    public async Task PutLogo_WhenHtmlNamedPng_ShouldBeValidation()
    {
        using var put = await PutAsync("<html><body><script>alert(1)</script></body></html>"u8.ToArray(), "logo.png");

        var error = await RefusalOfAsync(put);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("logo");
        error.Error.ShouldContain("PNG, JPEG, WebP or SVG");
    }

    [Test]
    public async Task PutLogo_WhenDtd_ShouldBeValidation()
    {
        var bomb = Encoding.UTF8.GetBytes($"<?xml version=\"1.0\"?><!DOCTYPE svg [<!ENTITY a \"aaaa\"><!ENTITY b \"&a;&a;&a;&a;\">]><svg xmlns=\"{SvgNs}\"><text>&b;</text></svg>");

        using var put = await PutAsync(bomb, "logo.svg");

        var error = await RefusalOfAsync(put);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("logo");
        error.Error.ShouldContain("PNG, JPEG, WebP or SVG");
    }

    [Test]
    public async Task PutLogo_WhenOverOneMib_ShouldBeValidation()
    {
        using var put = await PutAsync(Png(10, 10, 1024 * 1024 + 1));

        var error = await RefusalOfAsync(put);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("logo");
        error.Error.ShouldContain("at most 1 MB");
    }

    [TestCase(4097u, 10u)]
    [TestCase(10u, 4097u)]
    public async Task PutLogo_WhenSideOver4096_ShouldBeValidation(uint width, uint height)
    {
        using var put = await PutAsync(Png(width, height));

        var error = await RefusalOfAsync(put);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("logo");
        error.Error.ShouldContain("4096 pixels");
    }

    [Test]
    public async Task PutLogo_WhenTruncated_ShouldBeValidation()
    {
        using var put = await PutAsync(Png(10, 10)[..16]);

        var error = await RefusalOfAsync(put);

        error.Code.ShouldBe(ApiErrorCodes.Validation);
        error.Field.ShouldBe("logo");
        error.Error.ShouldContain("damaged or cut short");
    }

    [Test]
    public async Task PutLogo_WhenMember_ShouldBeForbidden()
    {
        var before = await RowAsync();

        using var put = await PutAsync(Png(10, 10), actingAs: SeedIds.Users.Mei);

        var error = await ApiClient.ReadApiErrorAsync(put, HttpStatusCode.Forbidden);
        error.Code.ShouldBe(ApiErrorCodes.Forbidden);
        (await RowAsync()).ShouldBe(before);
    }

    [Test]
    public async Task GetLogo_ShouldCarryNosniffAndSandbox()
    {
        (await PutAsync(Svg("<rect width=\"4\" height=\"4\"/>"), "logo.svg")).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var served = await Api.ActingAs(TestUser.Mei).GetAsync(LogoPath);

        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        served.Headers.GetValues("Content-Security-Policy").Single().ShouldStartWith("sandbox; default-src 'none'; style-src 'unsafe-inline'; img-src data:");
        served.Headers.GetValues("Cross-Origin-Resource-Policy").ShouldBe(["same-origin"]);
        served.Content.Headers.GetValues("Content-Disposition").ShouldBe(["inline; filename=\"logo\""]);
    }

    [Test]
    public async Task GetLogo_WhenNotMember_ShouldBeNotFound()
    {
        (await PutAsync(Png(10, 10))).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var control = await Api.ActingAs(TestUser.Mei).GetAsync(LogoPath);
        using var outsider = await Api.ActingAs(TestUser.Kai).GetAsync(LogoPath);

        control.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await ApiClient.ReadApiErrorAsync(outsider, HttpStatusCode.NotFound)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task DeleteLogo_ShouldClearTheReferenceAndKeepTheBytes()
    {
        (await PutAsync(Png(10, 10))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var key = await StoredBlobKeyAsync();
        var before = (await RowAsync()).Version;

        using var deleted = await Api.DeleteAsync(LogoPath);
        using var again = await Api.DeleteAsync(LogoPath);

        deleted.StatusCode.ShouldBe(HttpStatusCode.OK);
        var brand = JsonDocument.Parse(await deleted.Content.ReadAsStringAsync()).RootElement;
        brand.GetProperty("logoUrl").ValueKind.ShouldBe(JsonValueKind.Null);
        (await RowAsync()).ShouldBe((null, before + 1));
        again.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await RowAsync()).Version.ShouldBe(before + 1);
        (await Host.Services.GetRequiredService<ShipLean.Contracts.Blobs.IBlobStore>().ExistsAsync(key, CancellationToken.None)).ShouldBeTrue();
        using var gone = await Api.GetAsync(LogoPath);
        gone.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Test]
    public async Task Download_WhenTokenValid_ShouldServeAsAttachment()
    {
        var png = Png(321, 123);
        (await PutAsync(png)).StatusCode.ShouldBe(HttpStatusCode.OK);
        var (_, token) = await NewTokenAsync(SeedIds.Users.Mei);

        using var served = await DownloadAsync(token);

        served.StatusCode.ShouldBe(HttpStatusCode.OK);
        served.Content.Headers.GetValues("Content-Disposition").ShouldBe(["attachment; filename=\"logo\""]);
        served.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        served.Headers.CacheControl!.ToString().ShouldBe("no-cache, private");
        served.Content.Headers.ContentType!.MediaType.ShouldBe("image/png");
        (await served.Content.ReadAsByteArrayAsync()).ShouldBe(png);
    }

    [Test]
    public async Task Download_WhenLogoRemoved_ShouldBeNotFound()
    {
        (await PutAsync(Png(10, 10))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var (_, token) = await NewTokenAsync(SeedIds.Users.Mei);
        using var control = await DownloadAsync(token);
        control.StatusCode.ShouldBe(HttpStatusCode.OK);

        (await Api.DeleteAsync(LogoPath)).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var removed = await DownloadAsync(token);
        var error = await NotFoundOfAsync(removed);

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Error.ShouldBe(NotValid);
    }

    [Test]
    public async Task Download_WhenMemberLeft_ShouldBeNotFound()
    {
        (await PutAsync(Png(10, 10))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var (_, token) = await NewTokenAsync(SeedIds.Users.Priya);
        using var control = await DownloadAsync(token);
        control.StatusCode.ShouldBe(HttpStatusCode.OK);

        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync(
                "UPDATE project_member SET archived_at = now() WHERE user_id = @user AND project_id = @project", new { user = SeedIds.Users.Priya, project = SeedIds.Projects.Ship });
        }

        using var left = await DownloadAsync(token);
        var error = await NotFoundOfAsync(left);

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Error.ShouldBe(NotValid);
    }

    [Test]
    public async Task Download_WhenTokenExpired_ShouldBeNotFound()
    {
        (await PutAsync(Png(10, 10))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var (_, live) = await NewTokenAsync(SeedIds.Users.Mei);
        var (expiredItem, expired) = await NewTokenAsync(SeedIds.Users.Mei);
        await using (var database = await OpenDatabaseAsync())
        {
            await database.ExecuteAsync("UPDATE transfer_token SET expires_at = now() - interval '1 minute' WHERE work_item_id = @item", new { item = expiredItem });
        }

        using var control = await DownloadAsync(live);
        using var dead = await DownloadAsync(expired);

        control.StatusCode.ShouldBe(HttpStatusCode.OK);
        var error = await NotFoundOfAsync(dead);

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Error.ShouldBe(NotValid);
    }

    [Test]
    public async Task Download_ShouldNotBeSpentByUse()
    {
        (await PutAsync(Png(10, 10))).StatusCode.ShouldBe(HttpStatusCode.OK);
        var (_, token) = await NewTokenAsync(SeedIds.Users.Mei);

        using var first = await DownloadAsync(token);
        using var second = await DownloadAsync(token);
        using var wrongToken = await DownloadAsync("not-a-token");

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        var error = await NotFoundOfAsync(wrongToken);

        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Error.ShouldBe(NotValid);
    }
}
