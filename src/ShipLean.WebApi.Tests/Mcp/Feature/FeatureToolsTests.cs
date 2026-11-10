using Dapper;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Feature;

// set_status and create_feature in SHIP, as Joel with a read-write token.
[TestFixture]
public class FeatureToolsTests : McpFixture
{
    private string _featureKey = string.Empty;
    private string _storyKey = string.Empty;

    protected override bool WithProbeTools => false;

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        var feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Status me").InsertAsync(connection);
        var story = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle("Not a feature").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature });
        _storyKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @story", new { story });
    }

    [Test]
    public async Task SetStatus_WhenFeature_ShouldStampVerifiedViaSetStatus()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "set_status", $$"""{"feature":"{{_featureKey}}","status":"Waiting on the vendor"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        result.GetProperty("status").GetString().ShouldBe("Waiting on the vendor");
        result.GetProperty("revision").GetInt32().ShouldBeGreaterThan(1);
        var verified = result.GetProperty("verified");
        verified.GetProperty("via").GetString().ShouldBe("set_status");
        verified.GetProperty("by").GetString().ShouldBe("Joel Dickson");
        verified.GetProperty("stale").GetBoolean().ShouldBeFalse();
        verified.GetProperty("label").GetString().ShouldNotBeNullOrEmpty();
    }

    [Test]
    public async Task SetStatus_WhenNotFeature_ShouldReturnValidationError()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "set_status", $$"""{"feature":"{{_storyKey}}","status":"Nope"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
    }

    [Test]
    public async Task CreateFeature_WhenSlugOmitted_ShouldSuggestFromTitle()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "create_feature", """{"project":"SHIP","title":"Café Menu Rework!","goal":"Guests find dishes faster.","status":"Drafting"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        result.GetProperty("slug").GetString().ShouldBe("cafe-menu-rework");
        result.GetProperty("status").GetString().ShouldBe("Drafting");
        result.GetProperty("verified").GetProperty("via").GetString().ShouldBe("create_feature");
    }

    [Test]
    public async Task CreateFeature_WhenSlugTaken_ShouldReturnSlugTaken()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var first = await CallToolAsync(token, "create_feature", """{"project":"SHIP","title":"Taken name","goal":"One."}""");
        first.IsToolError().ShouldBeFalse(first.ResultText());

        var reply = await CallToolAsync(token, "create_feature", """{"project":"SHIP","title":"Other","slug":"taken-name","goal":"Two."}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.SlugTaken);
        error.GetProperty("field").GetString().ShouldBe("slug");
    }

    [Test]
    public async Task CreateFeature_WhenSlugEmpty_ShouldReturnValidationOnSlug()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "create_feature", """{"project":"SHIP","title":"Empty slug","slug":"","goal":"x"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("slug");
    }

    private async Task<(int Revision, string? Health, string StatusLine, string? Agent)> HealthStateAsync(string key)
    {
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync("SELECT revision, health, status_line AS statusLine, health_set_agent AS agent FROM work_item WHERE key = @key", new { key });
        return ((int)row.revision, (string?)row.health, (string)row.statusline ?? string.Empty, (string?)row.agent);
    }

    private async Task<string> NewFeatureKeyAsync(string title)
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle(title).InsertAsync(connection);
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
    }

    [Test]
    public async Task SetStatus_WhenHealthGiven_ShouldSetHealthInTheSameRevision()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var key = await NewFeatureKeyAsync("Health in one write");
        var before = await HealthStateAsync(key);

        var reply = await CallToolAsync(token, "set_status", $$"""{"feature":"{{key}}","status":"Vendor slipped","health":"atRisk"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var after = await HealthStateAsync(key);
        after.Health.ShouldBe("atRisk");
        after.StatusLine.ShouldBe("Vendor slipped");
        after.Revision.ShouldBe(before.Revision + 1);
    }

    [Test]
    public async Task SetStatus_WhenHealthCaseDiffers_ShouldStillSetIt()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var key = await NewFeatureKeyAsync("Health case");

        var reply = await CallToolAsync(token, "set_status", $$"""{"feature":"{{key}}","status":"Stuck","health":"BLOCKED"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        (await HealthStateAsync(key)).Health.ShouldBe("blocked");
    }

    [TestCase("sunny")]
    [TestCase("1")]
    [TestCase("onTrack,atRisk")]
    public async Task SetStatus_WhenHealthInvalid_ShouldReturnValidationOnHealth(string health)
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var key = await NewFeatureKeyAsync("Health invalid");
        var before = await HealthStateAsync(key);

        var reply = await CallToolAsync(token, "set_status", $$"""{"feature":"{{key}}","status":"Fine","health":"{{health}}"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("health");
        (await HealthStateAsync(key)).ShouldBe(before);
    }

    [Test]
    public async Task SetStatus_WhenHealthGiven_ShouldStampTheAgent()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var key = await NewFeatureKeyAsync("Health agent");

        var reply = await CallToolAsync(token, "set_status", $$"""{"feature":"{{key}}","status":"On it","health":"onTrack"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        (await HealthStateAsync(key)).Agent.ShouldBe("Test Agent (unverified)");
    }

    [Test]
    public async Task SetStatus_WhenHealthOmitted_ShouldLeaveHealthAlone()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var key = await NewFeatureKeyAsync("Health kept");
        (await CallToolAsync(token, "set_status", $$"""{"feature":"{{key}}","status":"One","health":"atRisk"}""")).IsToolError().ShouldBeFalse();

        var reply = await CallToolAsync(token, "set_status", $$"""{"feature":"{{key}}","status":"Two, blocked on the vendor"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        (await HealthStateAsync(key)).Health.ShouldBe("atRisk");
    }

    [Test]
    public async Task CreateFeature_WhenHealthGiven_ShouldCreateWithHealth()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "create_feature", """{"project":"SHIP","title":"Born at risk","goal":"Prove health at create.","health":"atRisk"}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var key = reply.Structured().GetProperty("key").GetString()!;
        var created = await HealthStateAsync(key);
        created.Health.ShouldBe("atRisk");
        created.Revision.ShouldBe(1);
        created.Agent.ShouldBe("Test Agent (unverified)");
    }

    [Test]
    public async Task CreateFeature_WhenHealthOmitted_ShouldLeaveHealthNull()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "create_feature", """{"project":"SHIP","title":"Born plain","goal":"No health."}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        (await HealthStateAsync(reply.Structured().GetProperty("key").GetString()!)).Health.ShouldBeNull();
    }

    [Test]
    public async Task SetStatus_WhenNonMemberSendsInvalidHealth_ShouldReturnNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var foreignKey = await ForeignFeatureKeyAsync();

        var reply = await CallToolAsync(token, "set_status", $$"""{"feature":"{{foreignKey}}","status":"Fine","health":"sunny"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    [Test]
    public async Task CreateFeature_WhenNonMemberSendsInvalidHealth_ShouldReturnNotFound()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "create_feature", """{"project":"PART","title":"Not yours","goal":"x","health":"sunny"}""");

        reply.IsToolError().ShouldBeTrue();
        reply.Structured().GetProperty("error").GetProperty("code").GetString().ShouldBe(ApiErrorCodes.NotFound);
    }

    private async Task<string> ForeignFeatureKeyAsync()
    {
        await using var connection = await OpenDatabaseAsync();
        var id = await new WorkItemBuilder(SeedIds.Projects.Part).WithType("feature").WithTitle("Foreign feature").InsertAsync(connection);
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @id", new { id });
    }
}
