using System.Net;
using System.Security.Cryptography;
using System.Text;
using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Agents;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Fixtures;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Payloads;

// The real IToolPayloads behind PUT api/uploads/payloads/{token}: the body is validated before anything is stored, one PUT
// wins, a refused upload leaves the link usable, and FindAsync answers only the user, authorization and tool it was issued for.
// A test that disables a user or rotates a security stamp arranges a user of its own.
[TestFixture]
public class PayloadStoreTests : McpFixture
{
    private const string NotLive = "That upload link is not valid.";

    protected override bool WithProbeTools => false;

    private IToolPayloads Payloads => Host.Services.GetRequiredService<IToolPayloads>();

    [TestCase("""[{"project":"SHIP"}]""")]
    [TestCase("\"just a string\"")]
    [TestCase("""{"project":"SHIP"}{"project":"PART"}""")]
    [TestCase("""{"project":"SHIP",""")]
    public async Task PayloadUpload_WhenBodyIsNotJsonObject_ShouldRefuse(string body)
    {
        var issued = await IssueAsync(SeedIds.Users.Joel, null, "capture_work");

        using var response = await PutAsync(issued.Path, Encoding.UTF8.GetBytes(body));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await StoredBodyAsync(issued.PayloadId)).ShouldBeNull();
    }

    [Test]
    public async Task PayloadUpload_WhenFieldGiven_ShouldStoreRawText()
    {
        var markdown = Encoding.UTF8.GetBytes("# Notes\r\n\nShe said \"don't\" and {\"not\": json}.\n\n- one\n- two \\ three\n");
        var plain = await IssueAsync(SeedIds.Users.Joel, null, "attach_markdown", "content");
        var withBom = await IssueAsync(SeedIds.Users.Joel, null, "attach_markdown", "content");

        using var plainResponse = await PutAsync(plain.Path, markdown);
        using var bomResponse = await PutAsync(withBom.Path, [0xEF, 0xBB, 0xBF, .. markdown]);

        plainResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        bomResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var found = await Payloads.FindAsync(plain.PayloadId, "attach_markdown", SeedIds.Users.Joel, null);
        found.ShouldNotBeNull();
        found.Field.ShouldBe("content");
        found.Body.ShouldBe(markdown);
        (await Payloads.FindAsync(withBom.PayloadId, "attach_markdown", SeedIds.Users.Joel, null))!.Body.ShouldBe(markdown);
    }

    // Chunked (no Content-Length) and with a Content-Length.
    [TestCase(true)]
    [TestCase(false)]
    public async Task PayloadUpload_WhenBodyOverLimit_ShouldRefuseAndAllowRetry(bool chunked)
    {
        var issued = await IssueAsync(SeedIds.Users.Joel, null, "attach_markdown", "content");
        var tooBig = new byte[IToolPayloads.MaxBytes + 1];
        Array.Fill(tooBig, (byte)'a');
        using var content = new StreamContent(new MemoryStream(tooBig));
        content.Headers.ContentLength = chunked ? null : tooBig.Length;
        using var request = new HttpRequestMessage(HttpMethod.Put, issued.Path) { Content = content };
        request.Headers.TransferEncodingChunked = chunked;

        using var refused = await Client.SendAsync(request);

        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await StoredBodyAsync(issued.PayloadId)).ShouldBeNull();
        using var retry = await PutAsync(issued.Path, Encoding.UTF8.GetBytes("Shorter."));
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Test]
    public async Task PayloadUpload_WhenTwoUploadsRace_ShouldStoreOne()
    {
        var issued = await IssueAsync(SeedIds.Users.Joel, null, "attach_markdown", "content");
        var bodies = new[] { Encoding.UTF8.GetBytes("First."), Encoding.UTF8.GetBytes("Second.") };

        var responses = await Task.WhenAll(bodies.Select(body => PutAsync(issued.Path, body)));

        responses.Select(r => r.StatusCode).Order().ToArray().ShouldBe(new[] { HttpStatusCode.OK, HttpStatusCode.NotFound });
        var winner = bodies[Array.FindIndex(responses, r => r.StatusCode == HttpStatusCode.OK)];
        (await StoredBodyAsync(issued.PayloadId)).ShouldBe(winner);
        foreach (var response in responses)
        {
            response.Dispose();
        }
    }

    [Test]
    public async Task PayloadUpload_WhenBodyInvalidUtf8_ShouldRefuseAndAllowRetry()
    {
        var issued = await IssueAsync(SeedIds.Users.Joel, null, "attach_markdown", "content");

        using var refused = await PutAsync(issued.Path, [0x48, 0x69, 0xC3, 0x28, 0xFF]);

        (await ApiClient.ReadApiErrorAsync(refused, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await StoredBodyAsync(issued.PayloadId)).ShouldBeNull();
        using var retry = await PutAsync(issued.Path, Encoding.UTF8.GetBytes("Hi é."));
        retry.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [TestCase("")]
    [TestCase("EFBBBF")]
    public async Task PayloadUpload_WhenBodyEmpty_ShouldRefuse(string hex)
    {
        var issued = await IssueAsync(SeedIds.Users.Joel, null, "attach_markdown", "content");

        using var response = await PutAsync(issued.Path, Convert.FromHexString(hex));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.BadRequest)).Code.ShouldBe(ApiErrorCodes.Validation);
        (await StoredBodyAsync(issued.PayloadId)).ShouldBeNull();
    }

    [Test]
    public async Task PayloadUpload_WhenTokenUnknown_ShouldReturnNotFound()
    {
        using var response = await PutAsync("/api/uploads/payloads/" + Convert.ToHexString(RandomNumberGenerator.GetBytes(32)), Encoding.UTF8.GetBytes("{}"));

        var error = await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound);
        error.Code.ShouldBe(ApiErrorCodes.NotFound);
        error.Error.ShouldBe(NotLive);
    }

    [Test]
    public async Task PayloadUpload_WhenAuthorizationRevokedBeforeUpload_ShouldReturnNotFound()
    {
        var authorizationId = await AuthorizeAsync(SeedIds.Users.Joel);
        var issued = await IssueAsync(SeedIds.Users.Joel, authorizationId);
        await ExecuteAsync("UPDATE oauth_authorization SET revoked_at = now() WHERE id = @authorizationId", new { authorizationId });

        using var response = await PutAsync(issued.Path, Encoding.UTF8.GetBytes("{}"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Error.ShouldBe(NotLive);
        (await StoredBodyAsync(issued.PayloadId)).ShouldBeNull();
    }

    [Test]
    public async Task PayloadUpload_WhenUserDisabledBeforeUpload_ShouldReturnNotFound()
    {
        var userId = await NewUserAsync();
        var issued = await IssueAsync(userId, await AuthorizeAsync(userId));
        await ExecuteAsync("UPDATE app_user SET disabled_at = now() WHERE id = @userId", new { userId });

        using var response = await PutAsync(issued.Path, Encoding.UTF8.GetBytes("{}"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Error.ShouldBe(NotLive);
        (await StoredBodyAsync(issued.PayloadId)).ShouldBeNull();
    }

    [Test]
    public async Task PayloadUpload_WhenSecurityStampChangedBeforeUpload_ShouldReturnNotFound()
    {
        var userId = await NewUserAsync();
        var issued = await IssueAsync(userId, await AuthorizeAsync(userId));
        await ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @userId", new { userId });

        using var response = await PutAsync(issued.Path, Encoding.UTF8.GetBytes("{}"));

        (await ApiClient.ReadApiErrorAsync(response, HttpStatusCode.NotFound)).Error.ShouldBe(NotLive);
        (await StoredBodyAsync(issued.PayloadId)).ShouldBeNull();
    }

    [Test]
    public async Task PayloadFind_WhenSecurityStampChanged_ShouldReturnNull()
    {
        var userId = await NewUserAsync();
        var authorizationId = await AuthorizeAsync(userId);
        var issued = await UploadedAsync(userId, authorizationId);
        await ExecuteAsync("UPDATE app_user SET security_stamp = 'rotated' WHERE id = @userId", new { userId });

        var found = await Payloads.FindAsync(issued.PayloadId, "capture_work", userId, authorizationId);

        found.ShouldBeNull();
    }

    [Test]
    public async Task PayloadFind_WhenAnotherUser_ShouldReturnNull()
    {
        var issued = await UploadedAsync(SeedIds.Users.Joel, null);

        var found = await Payloads.FindAsync(issued.PayloadId, "capture_work", SeedIds.Users.Priya, null);

        found.ShouldBeNull();
    }

    [Test]
    public async Task PayloadFind_WhenAnotherAuthorization_ShouldReturnNull()
    {
        var authorizationId = await AuthorizeAsync(SeedIds.Users.Joel);
        var other = await AuthorizeAsync(SeedIds.Users.Joel);
        var issued = await UploadedAsync(SeedIds.Users.Joel, authorizationId);
        var gateway = await UploadedAsync(SeedIds.Users.Joel, null);

        var viaOther = await Payloads.FindAsync(issued.PayloadId, "capture_work", SeedIds.Users.Joel, other);
        var viaGateway = await Payloads.FindAsync(issued.PayloadId, "capture_work", SeedIds.Users.Joel, null);
        var gatewayViaAgent = await Payloads.FindAsync(gateway.PayloadId, "capture_work", SeedIds.Users.Joel, authorizationId);

        viaOther.ShouldBeNull();
        viaGateway.ShouldBeNull();
        gatewayViaAgent.ShouldBeNull();
    }

    [Test]
    public async Task PayloadFind_WhenAuthorizationRevoked_ShouldReturnNull()
    {
        var authorizationId = await AuthorizeAsync(SeedIds.Users.Joel);
        var issued = await UploadedAsync(SeedIds.Users.Joel, authorizationId);
        await ExecuteAsync("UPDATE oauth_authorization SET revoked_at = now() WHERE id = @authorizationId", new { authorizationId });

        var found = await Payloads.FindAsync(issued.PayloadId, "capture_work", SeedIds.Users.Joel, authorizationId);

        found.ShouldBeNull();
    }

    [Test]
    public async Task PayloadFind_WhenUserDisabled_ShouldReturnNull()
    {
        var userId = await NewUserAsync();
        var issued = await UploadedAsync(userId, null);
        await ExecuteAsync("UPDATE app_user SET disabled_at = now() WHERE id = @userId", new { userId });

        var found = await Payloads.FindAsync(issued.PayloadId, "capture_work", userId, null);

        found.ShouldBeNull();
    }

    [Test]
    public async Task PayloadFind_WhenFoundTwice_ShouldReturnBothTimes()
    {
        var authorizationId = await AuthorizeAsync(SeedIds.Users.Joel);
        var issued = await UploadedAsync(SeedIds.Users.Joel, authorizationId);

        var first = await Payloads.FindAsync(issued.PayloadId, "capture_work", SeedIds.Users.Joel, authorizationId);
        var second = await Payloads.FindAsync(issued.PayloadId, "capture_work", SeedIds.Users.Joel, authorizationId);

        first.ShouldNotBeNull();
        second.ShouldNotBeNull();
        second.Body.ShouldBe(first.Body);
    }

    [Test]
    public async Task PayloadStore_WhenStoring_ShouldKeepOnlyTokenHash()
    {
        var authorizationId = await AuthorizeAsync(SeedIds.Users.Joel);
        var issued = await UploadedAsync(SeedIds.Users.Joel, authorizationId);
        var token = issued.Path.Split('/')[^1];

        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<string>("SELECT to_jsonb(p)::text FROM tool_payload p WHERE id = @id", new { id = issued.PayloadId });
        var hash = await connection.QuerySingleAsync<byte[]>("SELECT token_hash FROM tool_payload WHERE id = @id", new { id = issued.PayloadId });

        row.ShouldNotContain(token);
        row.ShouldNotContain(Convert.ToHexString(Encoding.UTF8.GetBytes(token)), Case.Insensitive);
        hash.ShouldBe(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }

    private async Task<Issued> IssueAsync(Guid userId, Guid? authorizationId, string tool = "capture_work", string? field = null)
    {
        var issued = await Payloads.IssueAsync(new NewToolPayload(tool, field, userId, authorizationId, "Test Agent"));
        return new Issued(issued.PayloadId, "/api/uploads/payloads/" + issued.UploadToken);
    }

    private async Task<Issued> UploadedAsync(Guid userId, Guid? authorizationId)
    {
        var issued = await IssueAsync(userId, authorizationId);
        using var response = await PutAsync(issued.Path, Encoding.UTF8.GetBytes("""{"project":"SHIP"}"""));
        response.EnsureSuccessStatusCode();
        return issued;
    }

    // An authorization of its own, made the way the OAuth flow makes one (it copies the user's security stamp).
    private async Task<Guid> AuthorizeAsync(Guid userId)
    {
        await using var scope = Host.Services.CreateAsyncScope();
        var client = await scope.ServiceProvider.GetRequiredService<IOAuthClients>()
            .RegisterAsync(new OAuthClientRegistration($"Payload Agent {Guid.NewGuid():N}", ["http://127.0.0.1:8123/callback"]));
        return await scope.ServiceProvider.GetRequiredService<IAgentAuthorizations>()
            .CreateAsync(userId, client.ClientId, new HashSet<string> { "shiplean:read", "shiplean:write" });
    }

    private async Task<Guid> NewUserAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        return await new AppUserBuilder().InsertAsync(connection);
    }

    private async Task<byte[]?> StoredBodyAsync(string payloadId)
    {
        await using var connection = await OpenDatabaseAsync();
        return await connection.QuerySingleAsync<byte[]?>("SELECT body FROM tool_payload WHERE id = @payloadId", new { payloadId });
    }

    private async Task ExecuteAsync(string sql, object parameters)
    {
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(sql, parameters);
    }

    private Task<HttpResponseMessage> PutAsync(string path, byte[] bytes) => Client.PutAsync(path, new ByteArrayContent(bytes));

    private sealed record Issued(string PayloadId, string Path);
}
