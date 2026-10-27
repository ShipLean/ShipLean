using System.Net;
using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Attach;

// read_attachment on a file it cannot return as text hands back a download URL, and GET /api/downloads/{token} serves the stored
// file as the user who asked, with the normal download's headers. The token is reusable until it expires.
[TestFixture]
public class AttachDownloadTests : McpFixture
{
    private static readonly byte[] PngBytes = Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

    private Guid _feature;
    private string _featureKey = string.Empty;

    protected override bool WithProbeTools => false;

    // The normal download is cookie-protected; the header comparison needs a signed-in caller. The download URL itself needs none.
    protected override TestUser SignedInAs => TestUser.Joel;

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        _feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Download from me").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature = _feature });
    }

    [Test]
    public async Task ReadAttachment_WhenPng_ShouldReturnDownloadUrl()
    {
        await AttachAsync("shot.png", "image/png", PngBytes);
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();

        var reply = await CallToolAsync(token, "read_attachment", $$"""{"item":"{{_featureKey}}","name":"shot.png"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        var url = result.GetProperty("download_url").GetString()!;
        new Uri(url).AbsolutePath.ShouldStartWith("/api/downloads/");
        result.GetProperty("command").GetString().ShouldBe($"curl -o <file> '{url}'");
        result.GetProperty("expires_at").GetDateTimeOffset().ShouldBeInRange(now.AddMinutes(9), now.AddMinutes(11));
        result.TryGetProperty("markdown", out _).ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        url.ShouldStartWith((await connection.QuerySingleAsync<string>("SELECT base_url FROM instance_settings")).TrimEnd('/') + "/api/downloads/");
        (await connection.ExecuteScalarAsync<string>("SELECT encode(token_hash, 'hex') FROM transfer_token WHERE kind = 'download' AND name = 'shot.png'"))!
            .ShouldNotContain(new Uri(url).Segments[^1]);
    }

    [Test]
    public async Task ReadAttachment_WhenSmallMarkdown_ShouldReturnTextAndNoUrl()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        (await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"small-note.md","content":"# Small\n\nQuokka."}""")).IsToolError().ShouldBeFalse();

        var reply = await CallToolAsync(token, "read_attachment", $$"""{"item":"{{_featureKey}}","name":"small-note.md"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        reply.Structured().GetProperty("markdown").GetString()!.ShouldContain("Quokka");
        reply.Structured().TryGetProperty("download_url", out _).ShouldBeFalse();
        reply.Structured().TryGetProperty("command", out _).ShouldBeFalse();
    }

    [Test]
    public async Task ReadAttachment_WhenMarkdownOver200Kb_ShouldReturnDownloadUrlInsteadOfText()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var text = new string('x', 210_000);
        (await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"big-note.md","content":"{{text}}"}""")).IsToolError().ShouldBeFalse();

        var reply = await CallToolAsync(token, "read_attachment", $$"""{"item":"{{_featureKey}}","name":"big-note.md"}""");

        reply.Structured().GetProperty("too_large").GetBoolean().ShouldBeTrue();
        reply.Structured().TryGetProperty("markdown", out _).ShouldBeFalse();
        using var response = await Client.GetAsync(PathOf(reply));
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsByteArrayAsync()).Length.ShouldBe(210_000);
    }

    [Test]
    public async Task Download_WhenTokenLive_ShouldReturnStoredBytesAndAllowReuse()
    {
        var bytes = new byte[300_000];
        Random.Shared.NextBytes(bytes);
        await AttachAsync("data.bin", "application/x-test", bytes);
        var path = await DownloadPathAsync("data.bin");

        using var first = await Client.GetAsync(path);
        using var second = await Client.GetAsync(path);

        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await first.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
        second.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await second.Content.ReadAsByteArrayAsync()).ShouldBe(bytes);
    }

    [TestCase("hdr.png", "image/png")]
    [TestCase("hdr.bin", "application/x-test")]
    [TestCase("hdr.svg", "image/svg+xml")]
    public async Task Download_WhenServed_ShouldCarryTheNormalDownloadsHeaders(string name, string contentType)
    {
        var bytes = contentType == "image/svg+xml" ? Encoding.UTF8.GetBytes("""<svg xmlns="http://www.w3.org/2000/svg" width="1" height="1"/>""") : PngBytes;
        var id = await AttachAsync(name, contentType, bytes);
        var path = await DownloadPathAsync(name);

        using var normal = await Client.GetAsync($"/api/attachments/{id}/content");
        using var viaToken = await Client.GetAsync(path);

        normal.StatusCode.ShouldBe(HttpStatusCode.OK);
        viaToken.StatusCode.ShouldBe(HttpStatusCode.OK);
        HeadersOf(viaToken).ShouldBe(HeadersOf(normal));
        viaToken.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        viaToken.Content.Headers.ContentDisposition!.DispositionType.ShouldBe(contentType == "application/x-test" ? "attachment" : "inline");
    }

    [Test]
    public async Task Download_WhenTokenUnknown_ShouldBeNotFound()
    {
        using var response = await Client.GetAsync("/api/downloads/not-a-real-token");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundCodeAsync(response);
    }

    [Test]
    public async Task Download_WhenTokenExpired_ShouldBeNotFound()
    {
        await AttachAsync("expired.bin", "application/x-test", [1]);
        var path = await DownloadPathAsync("expired.bin");
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE transfer_token SET expires_at = now() - interval '1 minute' WHERE name = 'expired.bin'");
        }

        using var response = await Client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundCodeAsync(response);
    }

    [Test]
    public async Task Download_WhenAttachmentArchivedSinceIssuing_ShouldBeNotFound()
    {
        var id = await AttachAsync("archived.bin", "application/x-test", [1]);
        var path = await DownloadPathAsync("archived.bin");
        await using (var scope = Host.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<AttachmentStore>().ArchiveAsync(id);
        }

        using var response = await Client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundCodeAsync(response);
    }

    [Test]
    public async Task Download_WhenAttachmentReplacedSinceIssuing_ShouldBeNotFound()
    {
        await AttachAsync("replaced.bin", "application/x-test", [1]);
        var path = await DownloadPathAsync("replaced.bin");
        await AttachAsync("replaced.bin", "application/x-test", [2, 3]);

        using var response = await Client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundCodeAsync(response);
    }

    [Test]
    public async Task Download_WhenMemberRemovedAfterIssuing_ShouldBeNotFound()
    {
        await AttachAsync("removed.bin", "application/x-test", [1]);
        var path = await DownloadPathAsync("removed.bin", TestUser.Priya);
        using (var before = await Client.GetAsync(path))
        {
            before.StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE project_member SET archived_at = now() WHERE user_id = @user AND project_id = @project", new { user = SeedIds.Users.Priya, project = SeedIds.Projects.Ship });
        }

        using var response = await Client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundCodeAsync(response);
    }

    private async Task<Guid> AttachAsync(string name, string contentType, byte[] bytes)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var added = await scope.ServiceProvider.GetRequiredService<AttachmentStore>()
            .AddAsync(_feature, name, contentType, new MemoryStream(bytes), new AttachmentSource("Test Agent (unverified)", "attach_file"));
        return added.Id;
    }

    private async Task<string> DownloadPathAsync(string name, TestUser? user = null)
    {
        var token = await TokenAsync(user ?? TestUser.Joel, ReadWrite);
        var reply = await CallToolAsync(token, "read_attachment", $$"""{"item":"{{_featureKey}}","name":"{{name}}"}""");
        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        return PathOf(reply);
    }

    private static string PathOf(McpReply reply) => new Uri(reply.Structured().GetProperty("download_url").GetString()!).AbsolutePath;

    private static string[] HeadersOf(HttpResponseMessage response)
        => response.Headers.Concat(response.Content.Headers)
            .Where(h => h.Key is "Content-Type" or "Content-Disposition" or "X-Content-Type-Options" or "Content-Security-Policy" or "Cache-Control" or "ETag" or "Content-Length")
            .OrderBy(h => h.Key, StringComparer.Ordinal).Select(h => $"{h.Key}: {string.Join(",", h.Value)}").ToArray();

    private static async Task ExpectNotFoundCodeAsync(HttpResponseMessage response)
    {
        (await ApiClient.ReadApiErrorAsync(response)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }
}
