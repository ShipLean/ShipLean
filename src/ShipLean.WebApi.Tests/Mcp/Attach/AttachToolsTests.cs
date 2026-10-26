using Dapper;
using Microsoft.Extensions.DependencyInjection;
using ShipLean.Application.Features.WorkItemDetails.Attachments;
using ShipLean.Contracts.Errors;
using ShipLean.TestSupport;
using ShipLean.TestSupport.Builders;
using ShipLean.TestSupport.Hosting;
using ShipLean.WebApi.Tests.Mcp.Transport;

namespace ShipLean.WebApi.Tests.Mcp.Attach;

// attach_markdown and attach_file in SHIP, as Joel with a read-write token. The per-file limit is 100 bytes, so a request
// over it still passes the /mcp body cap.
[TestFixture]
public class AttachToolsTests : McpFixture
{
    private const string AgentName = "Test Agent (unverified)";

    private Guid _feature;
    private string _featureKey = string.Empty;

    protected override bool WithProbeTools => false;

    protected override Action<IServiceCollection>? ConfigureTestServices => services =>
    {
        base.ConfigureTestServices!(services);
        services.AddSingleton(new AttachmentLimits(100));
    };

    [OneTimeSetUp]
    public async Task ArrangeItems()
    {
        await using var connection = await OpenDatabaseAsync();
        _feature = await new WorkItemBuilder(SeedIds.Projects.Ship).WithType("feature").WithTitle("Attach to me").InsertAsync(connection);
        _featureKey = await connection.QuerySingleAsync<string>("SELECT key FROM work_item WHERE id = @feature", new { feature = _feature });
    }

    [Test]
    public async Task AttachMarkdown_ShouldCreateMarkdownAttachmentStampedWithTool()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"design.md","content":"# Design\n\nBody."}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        result.GetProperty("name").GetString().ShouldBe("design.md");
        result.GetProperty("kind").GetString().ShouldBe("markdown");
        result.GetProperty("via").GetString().ShouldBe("attach_markdown");
        result.GetProperty("replaced").GetBoolean().ShouldBeFalse();
        await using var connection = await OpenDatabaseAsync();
        var row = await connection.QuerySingleAsync<(string? AgentName, string? Via, string? SearchText)>(
            "SELECT agent_name AS AgentName, uploaded_via AS Via, search_text AS SearchText FROM attachment WHERE id = @id",
            new { id = result.GetProperty("attachment_id").GetGuid() });
        row.AgentName.ShouldBe(AgentName);
        row.Via.ShouldBe("attach_markdown");
        row.SearchText.ShouldBe("# Design\n\nBody.");
    }

    [Test]
    public async Task AttachMarkdown_WhenNameNotMarkdown_ShouldReturnValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "attach_markdown", $$"""{"feature":"{{_featureKey}}","name":"design.txt","content":"x"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("name");
    }

    [Test]
    public async Task AttachFile_WhenValidBase64_ShouldCreateFileAttachmentStampedWithTool()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{_featureKey}}","name":"notes.txt","content_type":"text/plain","base64":"aGVsbG8="}""");

        reply.IsToolError().ShouldBeFalse(reply.ResultText());
        var result = reply.Structured();
        result.GetProperty("kind").GetString().ShouldBe("file");
        result.GetProperty("size_bytes").GetInt64().ShouldBe(5);
        result.GetProperty("via").GetString().ShouldBe("attach_file");
    }

    [Test]
    public async Task AttachFile_WhenBase64Invalid_ShouldReturnValidation()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);

        var reply = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{_featureKey}}","name":"bad.bin","content_type":"application/octet-stream","base64":"not base64!!"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("field").GetString().ShouldBe("base64");
    }

    [Test]
    public async Task AttachFile_WhenOverLimit_ShouldReturnTooLarge()
    {
        var token = await TokenAsync(TestUser.Joel, ReadWrite);
        var base64 = Convert.ToBase64String(new byte[101]);

        var reply = await CallToolAsync(token, "attach_file", $$"""{"feature":"{{_featureKey}}","name":"big.bin","content_type":"application/octet-stream","base64":"{{base64}}"}""");

        reply.IsToolError().ShouldBeTrue();
        var error = reply.Structured().GetProperty("error");
        error.GetProperty("code").GetString().ShouldBe(ApiErrorCodes.Validation);
        error.GetProperty("message").GetString()!.ShouldContain("at most");
        await using var connection = await OpenDatabaseAsync();
        (await connection.ExecuteScalarAsync<int>("SELECT count(*)::int FROM attachment WHERE work_item_id = @feature AND name = 'big.bin'", new { feature = _feature })).ShouldBe(0);
    }
}
