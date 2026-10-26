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
}
