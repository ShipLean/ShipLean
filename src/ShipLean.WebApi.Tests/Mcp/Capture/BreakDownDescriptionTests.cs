using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Capture;

// break_down children carrying a description and tags, as Joel.
[TestFixture]
public class BreakDownDescriptionTests : McpFixture
{
    protected override bool WithProbeTools => false;

    private async Task<string> StoryAsync(string title)
    {
        await using var connection = await OpenDatabaseAsync();
        var parent = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("story").WithTitle(title).InsertAsync(connection);
        return await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @parent", new { parent });
    }

    [Test]
    public async Task BreakDown_ChildWithDescriptionAndTags_ShouldCreateThem()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var parentKey = await StoryAsync("Describe my children");

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parentKey}}","children":[{"title":"Described child","description":"## Why\n\nBecause.","tags":["wave:1","lane:C"]}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var key = reply.Structured().GetProperty("items")[0].GetProperty("key").GetString();
        await using var check = await OpenDatabaseAsync();
        var row = await check.QuerySingleAsync<(string? Description, string[] Tags)>(
            "SELECT description, tags FROM work_item WHERE key = @key", new { key });
        row.Description.ShouldBe("## Why\n\nBecause.");
        row.Tags.ShouldBe(["wave:1", "lane:C"], ignoreOrder: true);
    }

    [Test]
    public async Task BreakDown_ChildWithoutThem_ShouldBehaveAsBefore()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var parentKey = await StoryAsync("Plain children");

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parentKey}}","children":[{"title":"Plain child","points":2}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var key = reply.Structured().GetProperty("items")[0].GetProperty("key").GetString();
        await using var check = await OpenDatabaseAsync();
        var row = await check.QuerySingleAsync<(string? Description, string[] Tags)>(
            "SELECT description, tags FROM work_item WHERE key = @key", new { key });
        string.IsNullOrEmpty(row.Description).ShouldBeTrue();
        row.Tags.ShouldBeEmpty();
    }

    [Test]
    public async Task BreakDown_DescriptionOverTheLimit_ShouldBeValidationAndCreateNothing()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var parentKey = await StoryAsync("Too long children");
        var tooLong = new string('x', 100_001);

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parentKey}}","children":[{"title":"Fine child"},{"title":"Long child","description":"{{tooLong}}"}]}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("children");
        await using var check = await OpenDatabaseAsync();
        (await check.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE title IN ('Fine child', 'Long child')")).ShouldBe(0);
    }

    [Test]
    public async Task BreakDown_SeveralChildrenWithDescriptionsAndTags_ShouldKeepEachOwn()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var parentKey = await StoryAsync("Several described children");

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parentKey}}","children":[{"title":"Child one","description":"One.","tags":["wave:1"]},{"title":"Child two","description":"Two.","tags":["wave:2","lane:B"]},{"title":"Child three","tags":["extra"]}]}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        await using var check = await OpenDatabaseAsync();
        var rows = (await check.QueryAsync<(string Title, string? Description, string[] Tags)>(
            "SELECT title, description, tags FROM work_item WHERE title LIKE 'Child %' AND parent_id = (SELECT id FROM work_item WHERE key = @parentKey)", new { parentKey })).ToDictionary(r => r.Title);
        rows["Child one"].Description.ShouldBe("One.");
        rows["Child one"].Tags.ShouldBe(["wave:1"]);
        rows["Child two"].Description.ShouldBe("Two.");
        rows["Child two"].Tags.ShouldBe(["wave:2", "lane:B"], ignoreOrder: true);
        string.IsNullOrEmpty(rows["Child three"].Description).ShouldBeTrue();
        rows["Child three"].Tags.ShouldBe(["extra"]);
    }

    [Test]
    public async Task BreakDown_NullOrBlankTag_ShouldBeValidationAndCreateNothing()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var parentKey = await StoryAsync("Bad tag children");

        var reply = await CallToolAsync(token, "break_down", $$"""{"parent":"{{parentKey}}","children":[{"title":"Fine tagged child","tags":["ok"]},{"title":"Null tagged child","tags":[null]},{"title":"Blank tagged child","tags":["  "]}]}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("children");
        await using var check = await OpenDatabaseAsync();
        (await check.QuerySingleAsync<int>("SELECT count(*)::int FROM work_item WHERE title LIKE '%tagged child'")).ShouldBe(0);
    }
}
