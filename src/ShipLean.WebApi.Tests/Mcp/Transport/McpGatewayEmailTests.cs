using System.Net;
using System.Text.Json;
using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Features.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Transport;

// /mcp with SHIPLEAN_MCP_GATEWAY_EMAIL_HEADER set: that header is the person, whatever bearer comes with it. With no header a
// bearer token still signs its own user in.
[TestFixture]
public class McpGatewayEmailTests : McpFixture
{
    private const string Header = "x-user-email";

    private string _storyKey = string.Empty;

    protected override IReadOnlyDictionary<string, string?> HostSettings { get; } = new Dictionary<string, string?>
    {
        [McpGatewaySettings.EmailHeaderKey] = Header,
    };

    [OneTimeSetUp]
    public async Task ArrangeAStory()
    {
        await using var connection = await OpenDatabaseAsync();
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithTitle("Gateway story").InsertAsync(connection);
        _storyKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
    }

    [Test]
    public async Task Mcp_WhenHeaderFirstSeen_ShouldCreateTheUserOnceAndReuseIt()
    {
        const string email = "gateway.person@example.test";

        var first = await CallAsAsync("  gateway.person@example.test ", "probe_whoami");
        var second = await CallAsAsync("Gateway.Person@example.test", "probe_whoami");

        first.Status.ShouldBe(HttpStatusCode.OK);
        var id = first.ResultText().Split('|')[0];
        second.ResultText().ShouldBe($"{id}|||False");
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(int Count, string Name, string Email)>(
            """
            SELECT count(*)::int AS Count, min(name) AS Name, min(email) AS Email
            FROM app_user WHERE lower(email) = lower(@email)
            """,
            new { email });
        row.Count.ShouldBe(1);
        row.Name.ShouldBe("gateway.person");
        row.Email.ShouldBe(email);
    }

    [Test]
    public async Task Mcp_WhenHeader_ShouldListToolsReadAndGrantEveryScope()
    {
        var listed = await PostAsync(null, """{"jsonrpc":"2.0","id":1,"method":"tools/list"}""", WithHeader(TestUser.Joel.Email));
        var projects = await CallAsAsync(TestUser.Joel.Email, "list_projects");
        var write = await CallAsAsync(TestUser.Joel.Email, "probe_write");
        var plan = await CallAsAsync(TestUser.Joel.Email, "probe_plan", """{"plan":"abc"}""");

        listed.Status.ShouldBe(HttpStatusCode.OK);
        listed.Body.ShouldContain("list_projects");
        listed.Body.ShouldContain("comment");
        listed.Body.ShouldContain("plan_sprint");
        projects.Status.ShouldBe(HttpStatusCode.OK);
        projects.ResultText().ShouldContain("SHIP");
        write.ResultText().ShouldBe("written");
        plan.ResultText().ShouldBe("applied");
    }

    [Test]
    public async Task Mcp_WhenHeader_ShouldCommentAsThatPersonWithoutRenamingThem()
    {
        var who = await CallAsAsync(TestUser.Joel.Email, "probe_whoami");
        var comment = await CallAsAsync(TestUser.Joel.Email, "comment", $$"""{"item":"{{_storyKey}}","text":"Noted from the gateway."}""");
        var item = await CallAsAsync(TestUser.Joel.Email, "get_work_item", $$"""{"key":"{{_storyKey}}"}""");

        who.ResultText().ShouldBe($"{TestUser.Joel.Id}|||False");
        comment.IsToolError().ShouldBeFalse(comment.ResultText());
        AgentName(comment.Structured()).ShouldBeNull();
        var noted = item.Structured().GetProperty("comments").EnumerateArray().Single(c => c.GetProperty("body").GetString() == "Noted from the gateway.");
        noted.GetProperty("author").GetString().ShouldBe("Joel Dickson");
        AgentName(noted).ShouldBeNull();
        await using var connection = await OpenDatabaseAsync();
        (await connection.QuerySingleAsync<string>("SELECT name FROM app_user WHERE id = @id", new { id = TestUser.Joel.Id })).ShouldBe("Joel Dickson");
    }

    [Test]
    public async Task Mcp_WhenBearerValidAndNoHeader_ShouldActAsTheTokenUser()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await CallToolAsync(token, "probe_whoami");

        reply.ResultText().ShouldBe($"{TestUser.Mei.Id}|Test Agent||False");
    }

    [Test]
    public async Task Mcp_WhenHeaderAndValidBearer_ShouldActAsTheHeaderPerson()
    {
        var token = await TokenAsync(TestUser.Mei, ReadOnly);

        var reply = await CallToolAsync(token, "probe_whoami", "{}", WithHeader(TestUser.Joel.Email));

        reply.ResultText().ShouldBe($"{TestUser.Joel.Id}|||False");
    }

    [Test]
    public async Task Mcp_WhenHeaderAndThirdPartyBearer_ShouldActAsTheHeaderPerson()
    {
        var reply = await CallToolAsync("third-party-identity-token", "probe_whoami", "{}", WithHeader(TestUser.Joel.Email));

        reply.Status.ShouldBe(HttpStatusCode.OK);
        reply.ResultText().ShouldBe($"{TestUser.Joel.Id}|||False");
    }

    [Test]
    public async Task Mcp_WhenBearerInvalidAndHeaderUnusable_ShouldAnswerInvalidToken()
    {
        var reply = await CallToolAsync("not-a-real-token", "probe_whoami", "{}", WithHeader("not-an-email"));

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
        reply.WwwAuthenticate.ShouldContain("error=\"invalid_token\"");
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase("   ")]
    public async Task Mcp_WhenHeaderBlankAndNoToken_ShouldAnswer401(string? email)
    {
        var reply = await CallAsAsync(email, "probe_whoami");

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
        reply.WwwAuthenticate.ShouldNotContain("invalid_token");
        Log.Runs("Whoami").ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenHeaderRepeated_ShouldAnswer401()
    {
        var reply = await CallToolAsync(null, "probe_whoami", "{}", request =>
            request.Headers.TryAddWithoutValidation(Header, ["once.gateway@example.test", "twice.gateway@example.test"]));

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>(
            "SELECT count(*)::int FROM app_user WHERE lower(email) IN ('once.gateway@example.test', 'twice.gateway@example.test')")).ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenHeaderIsNotAnEmail_ShouldAnswer401()
    {
        var reply = await CallAsAsync("not-an-email", "probe_whoami");

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
        Log.Runs("Whoami").ShouldBe(0);
    }

    [Test]
    public async Task Mcp_WhenNameWasChanged_ShouldKeepIt()
    {
        const string email = "renamed.gateway@example.test";
        (await CallAsAsync(email, "probe_whoami")).Status.ShouldBe(HttpStatusCode.OK);
        await using var connection = await OpenDatabaseAsync();
        await connection.ExecuteAsync(
            "UPDATE app_user SET name = @name, initials = 'PC' WHERE lower(email) = lower(@email)",
            new { name = "Pat Changed", email });

        (await CallAsAsync(email, "probe_whoami")).Status.ShouldBe(HttpStatusCode.OK);

        (await connection.QuerySingleAsync<string>("SELECT name FROM app_user WHERE lower(email) = lower(@email)", new { email })).ShouldBe("Pat Changed");
    }

    [Test]
    public async Task Mcp_WhenPersonIsNotOnTheProject_ShouldBeNotFound()
    {
        var reply = await CallAsAsync("stranger.gateway@example.test", "get_work_item", $$"""{"key":"{{_storyKey}}"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task Mcp_WhenUserDisabled_ShouldAnswer401()
    {
        const string email = "disabled.gateway@example.test";
        await using var connection = await OpenDatabaseAsync();
        await new AppUserBuilder().WithEmail(email).WithName("Disabled Gateway").DisabledAt(DateTimeOffset.UnixEpoch).InsertAsync(connection);

        var reply = await CallAsAsync(email, "probe_whoami");

        reply.Status.ShouldBe(HttpStatusCode.Unauthorized);
        reply.WwwAuthenticate.ShouldNotContain("invalid_token");
        Log.Runs("Whoami").ShouldBe(0);
    }

    private Task<McpReply> CallAsAsync(string? email, string tool, string arguments = "{}")
        => CallToolAsync(null, tool, arguments, WithHeader(email));

    private static Action<HttpRequestMessage> WithHeader(string? email)
        => request =>
        {
            if (email is not null)
            {
                request.Headers.TryAddWithoutValidation(Header, email);
            }
        };

    private static string? AgentName(JsonElement element)
        => element.TryGetProperty("agent_name", out var name) && name.ValueKind == JsonValueKind.String ? name.GetString() : null;
}
