using System.Net;
using System.Net.Http.Json;
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

// attach_file without base64 asks for an upload URL, and PUT /api/uploads/{token} stores the file, as the user who asked and with
// no cookie, bearer or client header. The per-file limit is 2 MB, above the 1 MB /mcp body cap.
[TestFixture]
public class AttachUploadTests : McpFixture
{
    private const string AgentName = "Test Agent (unverified)";
    private const long Limit = 2 * 1024 * 1024;

    private Guid _feature;
    private string _featureKey = string.Empty;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton(new AttachmentLimits(Limit));
    };

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        _feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Upload to me").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature = _feature });
    }

    [Test]
    public async Task AttachFile_WhenNoBase64_ShouldReturnUploadUrlAndAttachNothing()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var now = Host.Services.GetRequiredService<TimeProvider>().GetUtcNow();

        var reply = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{_featureKey}}","name":"url-only.png","content_type":"image/png"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        var url = result.GetProperty("upload_url").GetString()!;
        new Uri(url).AbsolutePath.ShouldStartWith("/api/uploads/");
        result.GetProperty("command").GetString().ShouldBe($"curl -T <file> '{url}'");
        result.GetProperty("expires_at").GetDateTimeOffset().ShouldBeInRange(now.AddMinutes(9), now.AddMinutes(11));
        await using var connection = await OpenDatabaseAsync();
        url.ShouldStartWith((await connection.QuerySingleAsync<string>("SELECT base_url FROM instance_settings")).TrimEnd('/') + "/api/uploads/");
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE work_item_id = @feature AND name = 'url-only.png'", new { feature = _feature })).ShouldBe(0);
        (await connection.ExecuteScalarAsync<string>("SELECT encode(token_hash, 'hex') FROM transfer_token WHERE work_item_id = @feature AND name = 'url-only.png'", new { feature = _feature }))!
            .ShouldNotContain(new Uri(url).Segments[^1]);
    }

    [Test]
    public async Task AttachFile_WhenNoBase64AndBadName_ShouldReturnValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{_featureKey}}","name":"  ","content_type":"image/png"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("field").GetString().ShouldBe("name");
    }

    [Test]
    public async Task Upload_WhenTokenLive_ShouldAttachAsAgentThroughAttachFile()
    {
        var path = await UploadPathAsync("upload.bin");
        var bytes = RandomBytes(1_500_000);

        using var response = await PutAsync(path, bytes);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadedBody>();
        body!.Name.ShouldBe("upload.bin");
        body.SizeBytes.ShouldBe(bytes.Length);
        body.Replaced.ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string AgentName, string Via, Guid UploadedBy, string ContentType, long Size)>(
            "SELECT agent_name AS AgentName, uploaded_via AS Via, uploaded_by AS UploadedBy, content_type AS ContentType, size_bytes AS Size FROM attachment WHERE id = @id", new { id = body.Id });
        row.ShouldBe((AgentName, "attach_file", SeedIds.Users.Joel, "application/x-test", bytes.Length));
    }

    [Test]
    public async Task Upload_WhenNameAlreadyOnItem_ShouldReplaceIt()
    {
        var first = await (await PutAsync(await UploadPathAsync("twice.txt"), [1, 2, 3])).Content.ReadFromJsonAsync<UploadedBody>();

        using var response = await PutAsync(await UploadPathAsync("twice.txt"), [4, 5]);

        var second = await response.Content.ReadFromJsonAsync<UploadedBody>();
        second!.Replaced.ShouldBeTrue();
        second.Id.ShouldNotBe(first!.Id);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE work_item_id = @feature AND name = 'twice.txt' AND archived_at IS NULL", new { feature = _feature })).ShouldBe(1);
    }

    [Test]
    public async Task Upload_WhenTokenSpent_ShouldBeNotFound()
    {
        var path = await UploadPathAsync("spent.txt");
        (await PutAsync(path, [1])).StatusCode.ShouldBe(HttpStatusCode.OK);

        using var response = await PutAsync(path, [2]);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundAsync(response);
    }

    [Test]
    public async Task Upload_WhenTwoPutsRaceOnOneToken_ShouldStoreOneAndRefuseTheOther()
    {
        var path = await UploadPathAsync("race.bin");

        var responses = await Task.WhenAll(Enumerable.Range(0, 2).Select(async _ =>
        {
            using var response = await PutAsync(path, new byte[1024]);
            return response.StatusCode;
        }));

        responses.Order().ShouldBe([HttpStatusCode.OK, HttpStatusCode.NotFound]);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE name = 'race.bin'")).ShouldBe(1);
    }

    [Test]
    public async Task Upload_WhenTokenUnknown_ShouldBeNotFound()
    {
        using var response = await PutAsync("/api/uploads/not-a-real-token", [1]);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundAsync(response);
    }

    [Test]
    public async Task Upload_WhenTokenExpired_ShouldBeNotFoundAndStoreNothing()
    {
        var path = await UploadPathAsync("expired.txt");
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE transfer_token SET expires_at = now() - interval '1 minute' WHERE name = 'expired.txt'");
        }

        using var response = await PutAsync(path, [1]);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundAsync(response);
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE name = 'expired.txt'")).ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenOverFileLimit_ShouldFailAndLeaveTokenUsable()
    {
        var path = await UploadPathAsync("big.bin");

        using var tooBig = await PutAsync(path, new byte[Limit + 1]);

        tooBig.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ApiError(tooBig)).Error.ShouldContain("at most");
        using var retry = await PutAsync(path, new byte[Limit]);
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task Upload_WhenStreamedBodyOverFileLimit_ShouldFailAndStoreNothing()
    {
        var path = await UploadPathAsync("streamed.bin");
        using var content = new StreamContent(new MemoryStream(new byte[Limit + 1]));
        content.Headers.ContentLength = null;
        using var request = new HttpRequestMessage(HttpMethod.Put, path) { Content = content };
        request.Headers.TransferEncodingChunked = true;

        using var response = await Client.SendAsync(request);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE name = 'streamed.bin'")).ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenMemberRemovedAfterIssuing_ShouldBeRefusedAndStoreNothing()
    {
        var path = await UploadPathAsync("removed.txt", TestUser.Priya);
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE project_member SET archived_at = now() WHERE user_id = @user AND project_id = @project", new { user = SeedIds.Users.Priya, project = SeedIds.Projects.Ship });
        }

        using var response = await PutAsync(path, [1]);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundAsync(response);
        await using var check = await OpenDatabaseAsync();
        (await check.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE name = 'removed.txt'")).ShouldBe(0);
    }

    [Test]
    public async Task Upload_WhenAgentRevokedAfterIssuing_ShouldBeNotFound()
    {
        var path = await UploadPathAsync("revoked.txt", TestUser.Joel, "Revoked Agent");
        await using (var connection = await OpenDatabaseAsync())
        {
            await connection.ExecuteAsync("UPDATE oauth_authorization SET revoked_at = now() WHERE id = (SELECT authorization_id FROM transfer_token WHERE name = 'revoked.txt')");
        }

        using var response = await PutAsync(path, [1]);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        await ExpectNotFoundAsync(response);
    }

    [Test]
    public async Task Upload_WhenSvgWithScript_ShouldStoreItSanitised()
    {
        var path = await UploadPathAsync("pic.svg", contentType: "image/svg+xml");
        var svg = """<svg xmlns="http://www.w3.org/2000/svg" width="10" height="10"><script>alert(1)</script><rect width="10" height="10" onclick="alert(2)"/></svg>""";

        using var response = await PutAsync(path, Encoding.UTF8.GetBytes(svg));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<UploadedBody>();
        await using var scope = Host.Services.CreateAsyncScope();
        var (attachment, content) = await scope.ServiceProvider.GetRequiredService<IAttachmentStore>().OpenAsync(body!.Id);
        await using var stream = content.Content;
        var stored = await new StreamReader(stream).ReadToEndAsync();
        attachment.ContentType.ShouldBe("image/svg+xml");
        stored.ShouldNotContain("script");
        stored.ShouldNotContain("onclick");
        stored.ShouldContain("rect");
    }

    private async Task<string> UploadPathAsync(string name, TestUser? user = null, string clientName = "Test Agent", string contentType = "application/x-test")
    {
        var token = await TokenAsync(user ?? TestUser.Joel, ReadWrite, clientName);
        var reply = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{_featureKey}}","name":"{{name}}","content_type":"{{contentType}}"}""");
        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        return new Uri(reply.Structured().GetProperty("upload_url").GetString()!).AbsolutePath;
    }

    private Task<HttpResponseMessage> PutAsync(string path, byte[] bytes) => Client.PutAsync(path, new ByteArrayContent(bytes));

    private static async Task ExpectNotFoundAsync(HttpResponseMessage response)
    {
        (await ApiError(response)).Code.ShouldBe(ApiErrorCodes.NotFound);
    }

    private static Task<ApiError> ApiError(HttpResponseMessage response) => ApiClient.ReadApiErrorAsync(response);

    private static byte[] RandomBytes(int length)
    {
        var bytes = new byte[length];
        Random.Shared.NextBytes(bytes);
        return bytes;
    }

    private sealed record UploadedBody(Guid Id, string Name, long SizeBytes, bool Replaced);
}
